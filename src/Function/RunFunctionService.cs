using Apiextensions.Fn.Proto.V1;
using EnumsNET;
using Function.SDK.CSharp.SourceGenerator.Models.svc.systems;
using Function.SDK.CSharp;
using Grpc.Core;
using k8s.Models;
using KubernetesCRDModelGen.Models.repo.github.upbound.io;
using static Apiextensions.Fn.Proto.V1.FunctionRunnerService;
using System.Text.Json;
using KubernetesCRDModelGen.Models.actions.github.upbound.io;

namespace Function;

public class RunFunctionService(ILogger<RunFunctionService> logger) : FunctionRunnerServiceBase
{
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
                            Name = repoName,
                            AllowAutoMerge = true,
                            AllowMergeCommit = false,
                            AllowRebaseMerge = false,
                            AllowSquashMerge = true,
                            Description = $"C# models for Kubernetes CRDs in {group.Key}",
                            Private = false,
                            Template =
                            [
                                new()
                                {
                                    Owner = "IvanJosipovic",
                                    Repository = "KubernetesCRDModelGen.Models.Template"
                                }
                            ]
                        }
                    }
                };

                resp.Desired.AddOrUpdate("repo-" + group.Key, repo);

                var policy = new V1alpha1RepositoryRuleset()
                {
                    Spec = new()
                    {
                        ForProvider = new()
                        {
                            BypassActors =
                            [
                                new()
                                {
                                    ActorType = "OrganizationAdmin",
                                    ActorId = 1,
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
                                    BranchNamePattern =
                                    [
                                        new()
                                        {
                                            Name = "main",
                                            Pattern = "main",
                                            Operator = "regex"
                                        }
                                    ],
                                    PullRequest =
                                    [
                                        new()
                                        {
                                            RequiredReviewThreadResolution = true
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
                                                    Context = "Create Release"
                                                }
                                            ]
                                        }
                                    ]
                                }
                            ]
                        }
                    }
                };

                resp.Desired.AddOrUpdate("policy-" + group.Key, policy);

                var existingFile = request.GetObservedResource<V1alpha1RepositoryFile?>("file-" + group.Key);

                var content = JsonSerializer.Serialize(new
                {
                    Config = group.Select(x => x)
                });

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
                            OverwriteOnCreate = existingFile?.Spec.ForProvider.Content != content,
                            Repository = repo.Spec.ForProvider.Name,
                            CommitMessage = "chore: update appsettings.json"
                        }
                    }
                };

                resp.Desired.AddOrUpdate("file-" + group.Key, file);

                resp.Requirements.Resources.Add("secret", new ResourceSelector()
                {
                    ApiVersion = V1Secret.KubeApiVersion,
                    Kind = V1Secret.KubeKind,
                    MatchName = observedXR.Spec.Credentials.SecretName,
                    Namespace = observedXR.Spec.Credentials.SecretNamespace
                });

                var requiredSecret = request.GetRequiredResource<V1Secret>("secret");

                if (requiredSecret != null)
                {
                    foreach (var item in requiredSecret.Data)
                    {
                        var secret = new V1alpha1ActionsSecret()
                        {
                            Spec = new()
                            {
                                ForProvider = new()
                                {
                                    SecretName = item.Key,
                                    PlaintextValueSecretRef = new()
                                    {
                                        Name = observedXR.Spec.Credentials.SecretNamespace,
                                        Key = item.Key,
                                        Namespace = observedXR.Spec.Credentials.SecretNamespace
                                    },
                                    Repository = repo.Spec.ForProvider.Name,
                                }
                            }
                        };

                        resp.Desired.AddOrUpdate("secret-" + secret.Spec.ForProvider.SecretName, secret);
                    }
                }
            }

            // Get Desired resources and update Status if Ready
            resp.UpdateDesiredReadyStatus(request, logger);

            return Task.FromResult(resp);
        }
    }
}
