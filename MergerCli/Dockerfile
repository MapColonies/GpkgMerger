#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
ARG VERSION="9.9.9.9"
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG VERSION
WORKDIR /src
COPY ["MergerCli/MergerCli.csproj", "MergerCli/"]
COPY ["MergerLogic/MergerLogic.csproj", "MergerLogic/"]
RUN dotnet restore "MergerCli/MergerCli.csproj"
COPY . .
WORKDIR "/src/MergerCli"
RUN echo version is $VERSION
RUN dotnet build "MergerCli.csproj" -c Release -o /app/build /p:Version=$VERSION

FROM build AS publish
RUN dotnet publish "MergerCli.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MergerCli.dll"]
