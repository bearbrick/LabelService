# 标签服务的 Linux 镜像（ADR 0008）。在 backend/ 目录构建：
#   docker build -f deploy/Dockerfile -t label-service .
# 镜像里不带字体（ADR 0011）：启动后在管理后台“素材与字体”页上传思源黑体等有授权的字体，存在数据卷的 fonts/ 里，立即生效。

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# 指定运行时标识时项目会打开 ReadyToRun（预编译），启动和第一个请求更快。
RUN dotnet publish src/LabelService.Api -c Release -r linux-x64 --self-contained false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# 数据目录（label.db、label-log.db、上传的素材和字体、备份、JWT 签名密钥）挂卷；镜像里先建好并交给 app 用户，命名卷第一次挂载时沿用这个属主。
RUN mkdir -p /data && chown app:app /data
ENV ASPNETCORE_URLS=http://+:8080 \
    LabelService__Data__Directory=/data
VOLUME /data
EXPOSE 8080
USER app
# 就绪检查：GET /health/ready（预热完成前 503）；存活检查：GET /health。镜像里没有 curl，由编排工具或负载均衡探测。
ENTRYPOINT ["dotnet", "LabelService.Api.dll"]
