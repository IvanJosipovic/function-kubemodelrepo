using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Apiextensions.Fn.Proto.V1;
using Function.SDK.CSharp;
using Function.SDK.CSharp.SourceGenerator.Models.svc.systems;
using Grpc.Core;
using k8s.Models;
using KubernetesCRDModelGen.Models.actions.github.upbound.io;
using KubernetesCRDModelGen.Models.http.crossplane.io;
using KubernetesCRDModelGen.Models.repo.github.upbound.io;
using static Apiextensions.Fn.Proto.V1.FunctionRunnerService;

namespace Function;

public class RunFunctionService(ILogger<RunFunctionService> logger) : FunctionRunnerServiceBase
{
    public static string ExternalName = "crossplane.io/external-name";

    public static JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

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
                            Description = $"C# models for Kubernetes CRDs in group {group.Key}",
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
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.Observe,
                            V1alpha1RepositoryRulesetSpecManagementPoliciesEnum.Create,
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
                            Repository = repoName,
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

                var appsettingsContent = JsonSerializer.Serialize(new
                {
                    Config = group.Select(x => x)
                }, jsonSerializerOptions);

                resp.AddFile(repoName, "appsettings.json", appsettingsContent, $"chore: update appsettings.json");

                var dotNetSDKVersion = "10.0.100-rc.2.25502.107";

                var global = $$"""
                    {
                      "sdk": {
                        "version": "{{dotNetSDKVersion}}",
                        "rollForward": "latestFeature",
                        "allowPrerelease": false
                      }
                    }
                    """;

                resp.AddFile(repoName, "global.json", global, $"chore: update .NET SDK to v{dotNetSDKVersion}");

                var deps = $"""
                    <Project>
                        <ItemGroup>
                            <PackageReference Include="KubernetesClient" Version="18.0.5" />
                            <PackageReference Include="KubernetesCRDModelGen.SourceGenerator" Version="1.1.0">
                                <PrivateAssets>all</PrivateAssets>
                            </PackageReference>
                        </ItemGroup>

                        <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
                            <PackageReference Include="System.Text.Json" Version="9.0.*" />
                        </ItemGroup>
                    </Project>
                    """;

                resp.AddFile(repoName, "Directory.Build.props", deps, $"fix: update dependencies");

                var csProj = $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                        <PropertyGroup>
                            <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
                            <PackageId>{repoName}</PackageId>
                            <RepositoryUrl>https://github.com/IvanJosipovic/{repoName}</RepositoryUrl>
                            <Description>C# models for Kubernetes CRDs in group {group.Key}</Description>
                            <Authors>Ivan Josipovic</Authors>
                            <PackageTags>Kubernetes CustomResourceDefinition CRD Models</PackageTags>
                            <ImplicitUsings>enable</ImplicitUsings>
                            <Nullable>enable</Nullable>
                            <LangVersion>latest</LangVersion>
                            <RepositoryType>git</RepositoryType>
                            <PackageLicenseExpression>MIT</PackageLicenseExpression>
                            <PublishRepositoryUrl>true</PublishRepositoryUrl>
                            <IncludeSymbols>true</IncludeSymbols>
                            <SymbolPackageFormat>snupkg</SymbolPackageFormat>
                            <IsPackable>true</IsPackable>
                            <GenerateDocumentationFile>true</GenerateDocumentationFile>
                            <PackageReadmeFile>README.md</PackageReadmeFile>
                            <NoWarn>$(NoWarn);CS1591;CS8618</NoWarn>
                            <WarningsAsErrors>$(WarningsAsErrors);CS8784;CS8785</WarningsAsErrors>
                        </PropertyGroup>

                        <ItemGroup>
                            <None Include="README.md" Pack="true" PackagePath="\" />
                            <AdditionalFiles Include="crds\*.yaml" />
                        </ItemGroup>
                    </Project>
                    """;

                resp.AddFile(repoName, repoName + ".csproj", csProj, $"fix: update .NET settings");

                var readme = $$"""
                    ## {{repoName}}
                    [![Nuget](https://img.shields.io/nuget/vpre/{{repoName}}.svg?style=flat-square)](https://www.nuget.org/packages/{{repoName}})[![Nuget)](https://img.shields.io/nuget/dt/{{repoName}}.svg?style=flat-square)](https://www.nuget.org/packages/{{repoName}})

                    C# models for Kubernetes CRDs in group {{group.Key}}
                    """;

                resp.AddFile(repoName, "README.md", readme, $"chore: update README.md");

                var cicd = """
                    name: CICD

                    on:
                      push:
                        branches:
                        - 'main'
                        - 'alpha'
                        - 'beta'
                      pull_request:
                        types: [opened, reopened, synchronize]
                      workflow_dispatch:

                    permissions:
                      id-token: write
                      contents: write
                      actions: write
                      checks: write
                      issues: write
                      pull-requests: write

                    jobs:
                      call-workflow:
                        uses: IvanJosipovic/KubernetesCRDModelGen/.github/workflows/cicd-template.yaml@main
                        secrets:
                          GH_TOKEN: ${{ secrets.GHPAT }}
                          NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
                    """;

                resp.AddFile(repoName, ".github/workflows/cicd.yaml", cicd, $"chore: update .github/workflows/cicd.yaml");

                var crdupdate = """
                    name: Update

                    on:
                      push:
                        branches:
                        - main
                      schedule:
                      - cron: "0 * * * *"
                      workflow_dispatch:

                    jobs:
                      call-workflow:
                        uses: IvanJosipovic/KubernetesCRDModelGen/.github/workflows/update-template.yaml@main
                        secrets:
                          GH_TOKEN: ${{secrets.GHPAT}}
                    """;

                resp.AddFile(repoName, ".github/workflows/update.yaml", crdupdate, $"chore: update .github/workflows/update.yaml");

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
                                Metadata = new()
                                {
                                    Annotations = new Dictionary<string, string>()
                                    {
                                        { ExternalName, repoName + ":" + data.Key }
                                    }
                                },
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
                                        Repository = repoName,
                                    }
                                }
                            };

                            resp.Desired.AddOrUpdate($"secret-{repoName}-{secretObj.Spec.ForProvider.SecretName}", secretObj);
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
                                            "default_workflow_permissions": "read",
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

public static class Extensions
{
    public static void AddFile(this RunFunctionResponse resp, string repository, string fileName, string content, string commitMessage)
    {
        var key = $"addFile-{repository}-{fileName}";

        var newFile = new V1alpha1RepositoryFile()
        {
            Metadata = new()
            {
                Annotations = new Dictionary<string, string>()
                {
                    { RunFunctionService.ExternalName, $"{repository}/{fileName}" }
                }
            },
            Spec = new()
            {
                ManagementPolicies =
                [
                    V1alpha1RepositoryFileSpecManagementPoliciesEnum.Observe,
                    V1alpha1RepositoryFileSpecManagementPoliciesEnum.Create,
                    V1alpha1RepositoryFileSpecManagementPoliciesEnum.Update,
                    V1alpha1RepositoryFileSpecManagementPoliciesEnum.LateInitialize
                ],
                ForProvider = new()
                {
                    Branch = "main",
                    Content = content,
                    File = fileName,
                    OverwriteOnCreate = false,
                    Repository = repository,
                    CommitMessage = commitMessage
                }
            }
        };

        resp.Desired.AddOrUpdate(key, newFile);
    }
}


