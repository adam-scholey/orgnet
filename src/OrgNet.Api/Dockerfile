FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/OrgNet.Api/OrgNet.Api.csproj", "src/OrgNet.Api/"]
COPY ["src/OrgNet.Infrastructure/OrgNet.Infrastructure.csproj", "src/OrgNet.Infrastructure/"]
COPY ["src/OrgNet.Domain/OrgNet.Domain.csproj", "src/OrgNet.Domain/"]
COPY ["src/OrgNet.Shared/OrgNet.Shared.csproj", "src/OrgNet.Shared/"]
RUN dotnet restore "src/OrgNet.Api/OrgNet.Api.csproj"
COPY . .
WORKDIR "/src/src/OrgNet.Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrgNet.Api.dll"]
