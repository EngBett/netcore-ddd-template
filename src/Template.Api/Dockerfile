FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# The aspnet images listen on 8080 by default from .NET 8 onward.
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# The build context is the repository root, where the projects live under src/.
# The image working directory is /build rather than /src so it does not read as a
# sibling of the repository's own src/ folder.
WORKDIR /build

# Restore needs every project file the API references, not just its own:
# Template.Api has ProjectReferences to the four sibling projects plus
# Template.ServiceDefaults, and `dotnet restore` fails outright if any of those
# .csproj files are missing. Copying just the project files first also keeps this
# layer cached until a dependency changes.
COPY ["global.json", "./"]
COPY ["src/Template.Api/Template.Api.csproj", "src/Template.Api/"]
COPY ["src/Template.Application/Template.Application.csproj", "src/Template.Application/"]
COPY ["src/Template.Common/Template.Common.csproj", "src/Template.Common/"]
COPY ["src/Template.Domain/Template.Domain.csproj", "src/Template.Domain/"]
COPY ["src/Template.Infrastructure/Template.Infrastructure.csproj", "src/Template.Infrastructure/"]
COPY ["aspire/Template.ServiceDefaults/Template.ServiceDefaults.csproj", "aspire/Template.ServiceDefaults/"]
RUN dotnet restore "src/Template.Api/Template.Api.csproj"

COPY . .
WORKDIR "/build/src/Template.Api"
RUN dotnet build "Template.Api.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "Template.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Template.Api.dll"]
