FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Catalog/Catalog.GRPC/Catalog.GRPC.csproj", "Catalog/Catalog.GRPC/"]
COPY ["Catalog/Catalog.Common/Catalog.Common.csproj", "Catalog/Catalog.Common/"]
COPY ["Contracts/Contracts.csproj", "Contracts/"]
RUN dotnet restore "Catalog/Catalog.GRPC/Catalog.GRPC.csproj"
COPY . .
WORKDIR "/src/Catalog/Catalog.GRPC"
RUN dotnet build "./Catalog.GRPC.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Catalog.GRPC.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.GRPC.dll"]
