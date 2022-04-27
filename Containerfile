FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -r linux-musl-x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true --self-contained true -p:PublishTrimmed=true -o out

# Build runtime image
FROM alpine
WORKDIR /
COPY --from=build-env /out .
ENTRYPOINT ["FScribe"]