FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
# The build context is the repository root, where the projects now live under src/.
# The image working directory is /build rather than /src so it does not read as a
# sibling of the repository's own src/ folder.
WORKDIR /build
COPY ["src/Template.Api/Template.Api.csproj", "src/Template.Api/"]
RUN dotnet restore "src/Template.Api/Template.Api.csproj"
COPY . .
WORKDIR "/build/src/Template.Api"
RUN dotnet build "Template.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Template.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Template.Api.dll"]
