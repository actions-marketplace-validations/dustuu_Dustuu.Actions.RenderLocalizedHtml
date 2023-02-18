# Set the base image as the .NET 7.0 SDK (this includes the runtime)
FROM mcr.microsoft.com/dotnet/sdk:7.0 as build-env

# Copy everything and publish the release (publish implicitly restores and builds)
WORKDIR /app
COPY . ./
RUN dotnet publish ./Dustuu.Actions.RenderLocalizedHtml/Dustuu.Actions.RenderLocalizedHtml.csproj -c Release -o out --no-self-contained

# Label the container
LABEL maintainer="Dustuu <meow@dustuu.cat>"
LABEL repository="https://github.com/dotnet/samples"
LABEL homepage="https://github.com/dotnet/samples"

# Label as GitHub action
LABEL com.github.actions.name="Render Localized HTML"
# Limit to 160 characters
LABEL com.github.actions.description="WIP action for rendering localized HTML files"
# See branding:
# https://docs.github.com/actions/creating-actions/metadata-syntax-for-github-actions#branding
LABEL com.github.actions.icon="globe"
LABEL com.github.actions.color="blue"

# Relayer the .NET Runtime, anew with the build output
FROM mcr.microsoft.com/dotnet/runtime:7.0
COPY --from=build-env /app/out .
ENTRYPOINT [ "dotnet", "/Dustuu.Actions.RenderLocalizedHtml.dll" ]