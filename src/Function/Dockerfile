# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/aspnet:9.0-alpine3.22@sha256:07c48612ac44393b15e741734761cf1f30cdb8f7e645e66e25b4563681ceef99 AS base
WORKDIR /app
EXPOSE 9443

# renovate: datasource=repology depName=alpine_3_22/busybox versioning=loose
ENV BUSYBOX_VERSION="1.37.0-r18"

# renovate: datasource=repology depName=alpine_3_22/busybox-binsh versioning=loose
ENV BUSYBOX_BINSH_VERSION="1.37.0-r18"

# renovate: datasource=repology depName=alpine_3_22/ca-certificates-bundle versioning=loose
ENV CA_CERTIFICATES_BUNDLES_VERSION="20250619-r0"

# renovate: datasource=repology depName=alpine_3_22/libcrypto3 versioning=loose
ENV LIBCRYPTO3_VERSION="3.5.2-r0"

# renovate: datasource=repology depName=alpine_3_22/libgcc versioning=loose
ENV LIBGCC_VERSION="14.2.0-r6"

# renovate: datasource=repology depName=alpine_3_22/libssl3 versioning=loose
ENV LIBSSL3_VERSION="3.5.2-r0"

# renovate: datasource=repology depName=alpine_3_22/libstdc++ versioning=loose
ENV LIBSTDC_VERSION="14.2.0-r6"

# renovate: datasource=repology depName=alpine_3_22/ssl_client versioning=loose
ENV SSL_VERSION="1.37.0-r18"

# renovate: datasource=repology depName=alpine_3_22/zlib versioning=loose
ENV ZLIB_VERSION="1.3.1-r2"

RUN apk add --upgrade --no-cache \
        busybox>$BUSYBOX_VERSION \
        busybox-binsh>$BUSYBOX_BINSH_VERSION \
        ca-certificates-bundle>$CA_CERTIFICATES_BUNDLES_VERSION \
        libcrypto3>$LIBCRYPTO3_VERSION \
        libgcc>$LIBGCC_VERSION \
        libssl3>$LIBSSL3_VERSION \
        libstdc++>$LIBSTDC_VERSION \
        ssl_client>$SSL_VERSION \
        zlib>$ZLIB_VERSION

USER $APP_UID

# This stage is used to build the service project
ARG BUILDPLATFORM=linux/amd64
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine3.22@sha256:512f8347b0d2f9848f099a8c31be07286955ceea337cadb1114057ed0b15862f AS build
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.1
WORKDIR /src
COPY ["Function.csproj", "."]
RUN dotnet restore "./Function.csproj"
COPY . .
RUN dotnet build "./Function.csproj" -c $BUILD_CONFIGURATION --use-current-runtime -o /app/build /p:Version=${VERSION}

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=0.0.1
RUN dotnet publish "./Function.csproj" -c $BUILD_CONFIGURATION --use-current-runtime -o /app/publish /p:UseAppHost=false /p:Version=${VERSION}

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final

WORKDIR /
COPY package.yaml .

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Function.dll"]
