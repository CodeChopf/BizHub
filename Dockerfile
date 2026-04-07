FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AuraPrints.Api/ .
RUN dotnet publish -c Release -r linux-x64 \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:OutputType=Exe \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0
WORKDIR /app
RUN adduser --disabled-password --gecos '' appuser
COPY --from=build /app/publish .
RUN chown -R appuser /app
USER appuser
ARG VERSION=dev
ENV BIZHUB_VERSION=$VERSION
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000
ENTRYPOINT ["./AuraPrintsApi"]
