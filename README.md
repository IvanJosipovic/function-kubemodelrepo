# FUNCTION


[CI/CD Pipeline](https://dev.azure.com/GalileoDigital/DA%20-%20Core%20Technology/_build?definitionId=2124)

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
docker build -t function-github src/Function
docker run -it -p 9443:9443 function-github
```

## Run Test
Then, in another terminal, call it with these example manifests
```
crossplane render example/xr.yaml example/composition.yaml example/functions.yaml
```

```yaml
---
TBD
```
