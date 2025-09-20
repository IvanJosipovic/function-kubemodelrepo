using EnumsNET;
using Function.SDK.CSharp.SourceGenerator.Models.svc.systems;
using KubernetesCRDModelGen.Models.repo.github.upbound.io;
using Shouldly;

namespace Function.DA.ETL.Tests;

public class UnitTest1
{
    [Fact]
    public void TestDesired()
    {
        var xr = new V1alpha1KubeModelRepo()
        {
            Metadata = new()
            {
                Name = "test",
                NamespaceProperty = "default"
            },
            Spec = new()
            {
                Repos =
                [
                    new()
                    {
                        Group = "test.com",
                        Oci = new()
                        {
                            Image = "imag.test.com",
                            SemVer = ">=2.0.0"
                        }
                    }
                ]
            }
        };

        var request = TestExtensions.GetFunctionRequest(xr);
        var response = request.GetTestResponse();

        var desiredResource = response.Desired.GetResource<V1alpha1Repository>("repo-test.com");

        var expectedResource = new V1alpha1Repository()
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
                    Name = "KubernetesCRDModelGen.Models.test.com",
                    AllowAutoMerge = true,
                    AllowMergeCommit = false,
                    AllowRebaseMerge = false,
                    AllowSquashMerge = true,
                    Description = $"C# models for Kubernetes CRDs in test.com",
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

        desiredResource.ShouldBeEquivalentTo(expectedResource);
    }
}
