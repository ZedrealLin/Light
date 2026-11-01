<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function logout() {
  localStorage.removeItem('light.adminToken')
  router.push('/login')
}
</script>

<template>
  <el-container class="layout">
    <el-aside width="200px" class="aside">
      <div class="brand">心灯 · 后台</div>
      <el-menu default-active="/alerts" router>
        <el-menu-item index="/alerts">预警工单</el-menu-item>
        <el-menu-item index="/users">用户管理</el-menu-item>
        <el-menu-item index="/scales">量表与关键词</el-menu-item>
      </el-menu>
      <div class="logout">
        <el-button text @click="logout">退出登录</el-button>
      </div>
    </el-aside>
    <el-main class="main">
      <router-view />
    </el-main>
  </el-container>
</template>

<style>
body {
  margin: 0;
  background: #f6f4f0;
  font-family: 'PingFang SC', 'Microsoft YaHei', system-ui, sans-serif;
  font-size: 15px;
  color: #4a4238;
}
.layout {
  min-height: 100vh;
}
.aside {
  background: #fffdf9;
  border-right: 1px solid #eee4d8;
  display: flex;
  flex-direction: column;
}
.brand {
  padding: 18px 16px;
  font-size: 17px;
  font-weight: 500;
  color: #c77e4e;
  border-bottom: 1px solid #eee4d8;
}
.aside .el-menu {
  border-right: none;
  flex: 1;
}
.logout {
  padding: 12px 16px;
}
.main {
  padding: 20px;
}
</style>
