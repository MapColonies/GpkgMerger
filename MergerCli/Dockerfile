#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

ARG VERSION=9.9.9.9

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["MergerCli/MergerCli.csproj", "MergerCli/"]
COPY ["MergerLogic/MergerLogic.csproj", "MergerLogic/"]
RUN dotnet restore "MergerCli/MergerCli.csproj"
COPY . .
WORKDIR "/src/MergerCli"
RUN dotnet build "MergerCli.csproj" -c Release -o /app/build /p:Version=${VERSION}

FROM build AS publish
RUN dotnet publish "MergerCli.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MergerCli.dll"]
