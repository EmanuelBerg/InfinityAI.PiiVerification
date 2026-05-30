# syntax=docker/dockerfile:1.7

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG TARGETARCH

WORKDIR /source

RUN --mount=type=secret,id=NUGET_TOKEN \
    dotnet nuget add source "https://nuget.pkg.github.com/emanuelberg/index.json" \
      --name github \
      --username emanuelberg \
      --password "$(cat /run/secrets/NUGET_TOKEN)" \
      --store-password-in-clear-text

COPY *.csproj .

RUN dotnet restore -a $TARGETARCH

COPY . .

RUN dotnet publish --no-restore \
    -a $TARGETARCH \
    -c Release \
    -o /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "InfinityAI.Component.PiiVerification.dll"]