# FUNCTION

## How to Test

You can run your function locally and test it using `crossplane render`
with the example manifests.

### Download Crank and rename to Crossplane
https://releases.crossplane.io/stable/current/bin

## Run Function In IDE
Download the lastest [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
```shell
dotnet debug
```

## Run Function In Docker
```shell
docker build -t function-kubemodelrepo src/Function
docker run -it -p 9443:9443 function-kubemodelrepo
```

## Run Test
Then, in another terminal, call it with these example manifests
```shell
crossplane render example/xr.yaml example/composition.yaml example/functions.yaml --required-resources example/extra/
```

```yaml
apiVersion: svc.systems/v1alpha1
kind: KubeModelRepo
metadata:
  name: main
status:
  conditions:
  - lastTransitionTime: "2024-01-01T00:00:00Z"
    message: 'Unready resources: action-permission-databricks.upbound.io, file-databricks.upbound.io,
      repo-databricks.upbound.io, and 3 more'
    reason: Creating
    status: "False"
    type: Ready
---
apiVersion: http.crossplane.io/v1alpha2
kind: Request
metadata:
  annotations:
    crossplane.io/composition-resource-name: action-permission-databricks.upbound.io
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  deletionPolicy: Orphan
  forProvider:
    headers:
      Accept:
      - application/vnd.github+json
      Authorization:
      - Bearer {{ kubemodelrepo:default:GHPAT }}
    mappings:
    - action: OBSERVE
      method: GET
      url: (.payload.baseUrl)
    - action: CREATE
      body: "{\r\n    \"default_workflow_permissions\": \"write\",\r\n    \"can_approve_pull_request_reviews\":
        false\r\n}"
      method: PUT
      url: (.payload.baseUrl)
    - action: UPDATE
      body: "{\r\n    \"default_workflow_permissions\": \"write\",\r\n    \"can_approve_pull_request_reviews\":
        false\r\n}"
      method: PUT
      url: (.payload.baseUrl)
    payload:
      baseUrl: https://api.github.com/repos/IvanJosipovic/KubernetesCRDModelGen.Models.databricks.upbound.io/actions/permissions/workflow
    waitTimeout: 1m
---
apiVersion: repo.github.upbound.io/v1alpha1
kind: RepositoryFile
metadata:
  annotations:
    crossplane.io/composition-resource-name: file-databricks.upbound.io
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  forProvider:
    branch: main
    commitMessage: 'chore: update appsettings.json'
    content: "{\r\n  \"Config\": [\r\n    {\r\n      \"group\": \"databricks.upbound.io\",\r\n      
      \     \"oci\": {\r\n        \"image\": \"xpkg.upbound.io/upbound/provider-databricks\",\r\n   
      \       \"semVer\": \"\\u003E=0.1.0\"\r\n      }\r\n    }\r\n  ]\r\n}"
    file: appsettings.json
    overwriteOnCreate: true
    repository: KubernetesCRDModelGen.Models.databricks.upbound.io
  managementPolicies:
  - Observe
  - Create
  - Update
  - LateInitialize
---
apiVersion: repo.github.upbound.io/v1alpha1
kind: Repository
metadata:
  annotations:
    crossplane.io/composition-resource-name: repo-databricks.upbound.io
    crossplane.io/external-name: KubernetesCRDModelGen.Models.databricks.upbound.io
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  forProvider:
    allowAutoMerge: true
    allowMergeCommit: false
    allowRebaseMerge: false
    allowSquashMerge: true
    allowUpdateBranch: true
    deleteBranchOnMerge: true
    description: C# models for Kubernetes CRDs in databricks.upbound.io
    hasDiscussions: true
    hasIssues: true
    hasWiki: false
    name: KubernetesCRDModelGen.Models.databricks.upbound.io
    private: false
    squashMergeCommitMessage: COMMIT_MESSAGES
    squashMergeCommitTitle: PR_TITLE
    template:
    - owner: IvanJosipovic
      repository: KubernetesCRDModelGen.Models.Template
    topics:
    - customresourcedefinition
    - kubernetes
    - model
    - dotnet
  managementPolicies:
  - Observe
  - Create
  - Update
  - LateInitialize
---
apiVersion: repo.github.upbound.io/v1alpha1
kind: RepositoryRuleset
metadata:
  annotations:
    crossplane.io/composition-resource-name: ruleset-databricks.upbound.io
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  forProvider:
    bypassActors:
    - actorId: 5
      actorType: RepositoryRole
      bypassMode: always
    conditions:
    - refName:
      - include:
        - ~DEFAULT_BRANCH
    enforcement: active
    name: main
    repository: KubernetesCRDModelGen.Models.databricks.upbound.io
    rules:
    - pullRequest:
      - dismissStaleReviewsOnPush: true
        requiredReviewThreadResolution: true
      requiredStatusChecks:
      - requiredCheck:
        - context: call-workflow / Create Release
          integrationId: 15368
    target: branch
  managementPolicies:
  - Create
  - Observe
  - Update
  - LateInitialize
---
apiVersion: actions.github.upbound.io/v1alpha1
kind: ActionsSecret
metadata:
  annotations:
    crossplane.io/composition-resource-name: secret-KubernetesCRDModelGen.Models.databricks.upbound.io-GHPAT
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  forProvider:
    plaintextValueSecretRef:
      key: GHPAT
      name: kubemodelrepo
      namespace: default
    repository: KubernetesCRDModelGen.Models.databricks.upbound.io
    secretName: GHPAT
  managementPolicies:
  - Observe
  - Create
  - Update
  - LateInitialize
---
apiVersion: actions.github.upbound.io/v1alpha1
kind: ActionsSecret
metadata:
  annotations:
    crossplane.io/composition-resource-name: secret-KubernetesCRDModelGen.Models.databricks.upbound.io-NUGET_API_KEY
  generateName: main-
  labels:
    crossplane.io/composite: main
  ownerReferences:
  - apiVersion: svc.systems/v1alpha1
    blockOwnerDeletion: true
    controller: true
    kind: KubeModelRepo
    name: main
    uid: ""
spec:
  forProvider:
    plaintextValueSecretRef:
      key: NUGET_API_KEY
      name: kubemodelrepo
      namespace: default
    repository: KubernetesCRDModelGen.Models.databricks.upbound.io
    secretName: NUGET_API_KEY
  managementPolicies:
  - Observe
  - Create
  - Update
  - LateInitialize
```
