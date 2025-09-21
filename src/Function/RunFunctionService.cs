using Apiextensions.Fn.Proto.V1;
using EnumsNET;
using Function.SDK.CSharp.SourceGenerator.Models.svc.systems;
using Function.SDK.CSharp;
using Google.Protobuf;
using Grpc.Core;
using k8s.Models;
using k8s;
using KubernetesCRDModelGen.Models.actions.github.upbound.io;
using KubernetesCRDModelGen.Models.http.crossplane.io;
using KubernetesCRDModelGen.Models.repo.github.upbound.io;
using static Apiextensions.Fn.Proto.V1.FunctionRunnerService;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Function;

public class RunFunctionService(ILogger<RunFunctionService> logger) : FunctionRunnerServiceBase
{
    public static string ExternalName = "crossplane.io/external-name";

    public static JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public override Task<RunFunctionResponse> RunFunction(RunFunctionRequest request, ServerCallContext context)
    {
        var resp = request.To();
        resp.Requirements = new();

        var observedXR = request.GetObservedCompositeResource<V1alpha1KubeModelRepo>();

        if (observedXR == null)
        {
            resp.Fatal("XR is null");
            return Task.FromResult(resp);
        }

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["xr-apiversion"] = observedXR.ApiVersion,
            ["xr-kind"] = observedXR.Kind,
            ["xr-name"] = observedXR.Name()
        }))
        {
            logger.LogInformation("Running Function");
            resp.Normal("Running Function");

            foreach (var group in observedXR.Spec.Repos.GroupBy(x => x.Group))
            {
                var repoName = $"KubernetesCRDModelGen.Models.{group.Key}";

                var repo = new V1alpha1Repository()
                {
                    Metadata = new()
                    {
                        Annotations = new Dictionary<string, string>()
                        {
                            { ExternalName, repoName }
                        }
                    },
                    Spec = new()
                    {
                        ManagementPolicies = [
                            V1alpha1RepositorySpecManagementPoliciesEnum.Observe,
                            V1alpha1RepositorySpecManagementPoliciesEnum.Create,
                            V1alpha1RepositorySpecManagementPoliciesEnum.Update,
                            V1alpha1RepositorySpecManagementPoliciesEnum.LateInitialize,
                        ],
                        ForProvider = new()
                        {
                            AllowAutoMerge = true,
                            AllowMergeCommit = false,
                            AllowRebaseMerge = false,
                            AllowSquashMerge = true,
                            AllowUpdateBranch = true,
                            DeleteBranchOnMerge = true,
                            Description = $"C# models for Kubernetes CRDs in {group.Key}",
                            HasDiscussions = true,
                            HasIssues = true,
                            HasWiki = false,
                            Name = repoName,
                            Private = false,
                            SquashMergeCommitMessage = "COMMIT_MESSAGES",
                            SquashMergeCommitTitle = "PR_TITLE",
                            Template =
                            [
                                new()
                                {
                                    Owner = "IvanJosipovic",
                                    Repository = "KubernetesCRDModelGen.Models.Template"
                                }
                            ],
                            Topics =
                            [
                                "customresourcedefinition",
                                "kubernetes",
                                "model",
                                "dotnet"
                            ]
                        }
                    }
                };

                resp.Desired.AddOrUpdate("repo-" + group.Key, repo);

                var ruleset = new V1alpha1RepositoryRuleset()
                {
                    Spec = new()
                    {
                        ManagementPolicies =
                        [
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.Create,
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.Observe,
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.Update,
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.LateInitialize
                        ],
                        ForProvider = new()
                        {
                            Conditions =
                            [
                                new()
                                {
                                    RefName =
                                    [
                                        new (){
                                            Include =
                                            [
                                                "~DEFAULT_BRANCH"
                                            ]
                                        }
                                    ]
                                }
                            ],
                            BypassActors =
                            [
                                new()
                                {
                                    ActorType = "RepositoryRole",
                                    ActorId = 5, //admin
                                    BypassMode = "always"
                                }
                            ],
                            Enforcement = "active",
                            Name = "main",
                            Repository = repo.Spec.ForProvider.Name,
                            Target = "branch",
                            Rules =
                            [
                                new()
                                {
                                    PullRequest =
                                    [
                                        new()
                                        {
                                            RequiredReviewThreadResolution = true,
                                            DismissStaleReviewsOnPush = true
                                        }
                                    ],
                                    RequiredStatusChecks =
                                    [
                                        new()
                                        {
                                            RequiredCheck =
                                            [
                                                new()
                                                {
                                                    Context = "call-workflow / Create Release",
                                                    IntegrationId = 15368 // Github Actions
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    }
                };

                resp.Desired.AddOrUpdate("ruleset-" + group.Key, ruleset);

                var existingFile = request.GetObservedResource<V1alpha1RepositoryFile?>("file-" + group.Key);

                var content = JsonSerializer.Serialize(new
                {
                    Config = group.Select(x => x)
                }, jsonSerializerOptions);

                var file = new V1alpha1RepositoryFile()
                {
                    Spec = new()
                    {
                        ManagementPolicies = [
                            V1alpha1RepositoryFileSpecManagementPoliciesEnum.Observe,
                            V1alpha1RepositoryFileSpecManagementPoliciesEnum.Create,
                            V1alpha1RepositoryFileSpecManagementPoliciesEnum.Update,
                            V1alpha1RepositoryFileSpecManagementPoliciesEnum.LateInitialize,
                        ],
                        ForProvider = new()
                        {
                            Branch = "main",
                            Content = content,
                            File = "appsettings.json",
                            OverwriteOnCreate = existingFile?.Status?.AtProvider?.Content != content,
                            Repository = repo.Spec.ForProvider.Name,
                            CommitMessage = "chore: update appsettings.json"
                        }
                    }
                };

                resp.Desired.AddOrUpdate("file-" + group.Key, file);

                resp.Requirements.Resources["secret"] = new ResourceSelector()
                {
                    ApiVersion = V1Secret.KubeApiVersion,
                    Kind = V1Secret.KubeKind,
                    MatchName = observedXR.Spec.Credentials.SecretName,
                    Namespace = observedXR.Spec.Credentials.SecretNamespace
                };

                var requiredSecret = request.GetRequiredResource<V1Secret>("secret");

                if (requiredSecret != null)
                {
                    foreach (var secret in requiredSecret.Where(x => x.Data != null))
                    {
                        foreach (var data in secret.Data)
                        {
                            var secretObj = new V1alpha1ActionsSecret()
                            {
                                Spec = new()
                                {
                                    ManagementPolicies =
                                    [
                                        V1alpha1ActionsSecretSpecManagementPoliciesEnum.Observe,
                                        V1alpha1ActionsSecretSpecManagementPoliciesEnum.Create,
                                        V1alpha1ActionsSecretSpecManagementPoliciesEnum.Update,
                                        V1alpha1ActionsSecretSpecManagementPoliciesEnum.LateInitialize
                                    ],
                                    ForProvider = new()
                                    {
                                        SecretName = data.Key,
                                        PlaintextValueSecretRef = new()
                                        {
                                            Name = observedXR.Spec.Credentials.SecretName,
                                            Key = data.Key,
                                            Namespace = observedXR.Spec.Credentials.SecretNamespace
                                        },
                                        Repository = repo.Spec.ForProvider.Name,
                                    }
                                }
                            };

                            resp.Desired.AddOrUpdate($"secret-{repo.Spec.ForProvider.Name}-{secretObj.Spec.ForProvider.SecretName}", secretObj);
                        }
                    }
                }

                // This API is not supported by the Provider
                //https://docs.github.com/en/rest/actions/permissions?apiVersion=2022-11-28#get-default-workflow-permissions-for-a-repository
                var secretSettings = new V1alpha2Request()
                {
                    Spec = new()
                    {
                        DeletionPolicy = V1alpha2RequestSpecDeletionPolicyEnum.Orphan,
                        ForProvider = new()
                        {
                            Headers = new Dictionary<string, IList<string>>()
                            {
                                { "Accept", ["application/vnd.github+json"] },
                                { "Authorization", [$$$"""Bearer {{ {{{observedXR.Spec.Credentials.SecretName}}}:{{{observedXR.Spec.Credentials.SecretNamespace}}}:GHPAT }}"""] },
                                //{ "X-GitHub-Api-Version", ["2022-11-28"] }
                            },
                            Payload = new()
                            {
                                BaseUrl = $"https://api.github.com/repos/IvanJosipovic/{repoName}/actions/permissions/workflow",
                            },
                            Mappings =
                            [
                                new()
                                {
                                    Action = V1alpha2RequestSpecForProviderMappingsActionEnum.OBSERVE,
                                    Method = V1alpha2RequestSpecForProviderMappingsMethodEnum.GET,
                                    Url = "(.payload.baseUrl)"
                                },
                                new()
                                {
                                    Action = V1alpha2RequestSpecForProviderMappingsActionEnum.CREATE,
                                    Method = V1alpha2RequestSpecForProviderMappingsMethodEnum.PUT,
                                    Url = "(.payload.baseUrl)",
                                    Body =
                                        """
                                        {
                                            "default_workflow_permissions": "write",
                                            "can_approve_pull_request_reviews": false
                                        }
                                        """
                                },
                                new()
                                {
                                    Action = V1alpha2RequestSpecForProviderMappingsActionEnum.UPDATE,
                                    Method = V1alpha2RequestSpecForProviderMappingsMethodEnum.PUT,
                                    Url = "(.payload.baseUrl)",
                                    Body =
                                        """
                                        {
                                            "default_workflow_permissions": "write",
                                            "can_approve_pull_request_reviews": false
                                        }
                                        """
                                }
                            ],
                            WaitTimeout = "1m"
                        }
                    }
                };

                resp.Desired.AddOrUpdate("action-permission-" + group.Key, secretSettings);
            }

            // Get Desired resources and update Status if Ready
            resp.UpdateDesiredReadyStatus(request, logger);

            return Task.FromResult(resp);
        }
    }
}
