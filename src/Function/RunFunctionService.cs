using Apiextensions.Fn.Proto.V1;
using EnumsNET;
using Function.SDK.CSharp;
using Function.SDK.CSharp.SourceGenerator.Models.svc.systems;
using Grpc.Core;
using k8s.Models;
using static Apiextensions.Fn.Proto.V1.FunctionRunnerService;
using KubernetesCRDModelGen.Models.repo.github.upbound.io;

namespace Function;

public class RunFunctionService(ILogger<RunFunctionService> logger) : FunctionRunnerServiceBase
{
    public override Task<RunFunctionResponse> RunFunction(RunFunctionRequest request, ServerCallContext context)
    {
        var resp = request.To(RequestExtensions.DefaultTTL);

        var observedXR = request.GetObservedCompositeResource<V1alpha1KubeModelRepo?>();

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

            foreach (var repo in observedXR.Spec.Repos)
            {
                var repoName = $"KubernetesCRDModelGen.Models.{repo.Group}";

                var model = new V1alpha1Repository()
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
                            Description = $"C# models for Kubernetes CRDs in {repo.Group}",
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

                resp.Desired.AddOrUpdate("repo-" + repo.Group, model);
            }

            // Get Desired resources and update Status if Ready
            resp.UpdateDesiredReadyStatus(request, logger);

            return Task.FromResult(resp);
        }
    }
}
