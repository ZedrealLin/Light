# 前端镜像（/web 与 /admin 共用）：Node 构建 + Nginx 托管
# build args: VITE_API_BASE（容器内由 Nginx 反代 /api → api 服务）
FROM node:22-alpine AS build
WORKDIR /src
ARG VITE_API_BASE=/api
ENV VITE_API_BASE=$VITE_API_BASE
COPY . .
RUN npm install && npm run build

FROM nginx:1.27-alpine
COPY --from=build /src/dist /usr/share/nginx/html
# SPA 路由回退 + /api 反代由宿主 Nginx 统一处理，这里仅托管静态文件
RUN printf 'server {\n  listen 80;\n  location / { root /usr/share/nginx/html; try_files $uri /index.html; }\n}\n' > /etc/nginx/conf.d/default.conf
EXPOSE 80
