FROM mcr.microsoft.com/dotnet/core/runtime:3.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.0-buster AS build
WORKDIR /src
COPY ["TankerkoenigToInfluxDb/TankerkoenigToInfluxDb.csproj", "TankerkoenigToInfluxDb/"]
RUN dotnet restore "TankerkoenigToInfluxDb/TankerkoenigToInfluxDb.csproj"
COPY . .
WORKDIR "/src/TankerkoenigToInfluxDb"
RUN dotnet build "TankerkoenigToInfluxDb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TankerkoenigToInfluxDb.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TankerkoenigToInfluxDb.dll"]