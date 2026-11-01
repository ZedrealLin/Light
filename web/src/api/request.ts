import axios from 'axios'
import type { InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'

/** 后端统一返回体（docs/CONVENTIONS.md 第 5 节） */
export interface ApiResponse<T = unknown> {
  code: number
  message: string
  data: T
  traceId: string
}

/** 业务错误：code 为后端错误码（见 CONVENTIONS 第 6 节） */
export class ApiError extends Error {
  readonly code: number

  constructor(code: number, message: string) {
    super(message)
    this.code = code
  }
}

/**
 * 统一请求封装：
 * - baseURL 默认指向本地后端，可用 VITE_API_BASE 覆盖（部署环境由 Nginx 反代 /api 时设为空字符串）
 * - 响应拦截统一处理：code=0 解包 data；非 0 统一提示并抛 ApiError；网络异常统一提示
 * - 401 跳登录在阶段 2 接入鉴权时补充
 */
const instance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE ?? 'http://127.0.0.1:5080',
  timeout: 15000,
})

instance.interceptors.request.use((config) => {
  const token = localStorage.getItem('light.accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// 401 时尝试用 refreshToken 刷新一次并重放原请求；失败则清理会话回登录页
let refreshing: Promise<boolean> | null = null

instance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const config = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined
    const status = error.response?.status
    if (status === 401 && config && !config._retried && !String(config.url).includes('/api/auth/')) {
      config._retried = true
      refreshing = refreshing ?? doRefresh()
      const ok = await refreshing
      refreshing = null
      if (ok) {
        const token = localStorage.getItem('light.accessToken')
        if (token) config.headers.Authorization = `Bearer ${token}`
        return instance.request(config)
      }
      window.location.hash = ''
      window.location.assign('/auth')
      return Promise.reject(error)
    }
    ElMessage.error('网络异常，请稍后再试')
    return Promise.reject(error)
  },
)

async function doRefresh(): Promise<boolean> {
  const refreshToken = localStorage.getItem('light.refreshToken')
  if (!refreshToken) return false
  try {
    const resp = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
      `${instance.defaults.baseURL}/api/auth/refresh`,
      { refreshToken },
    )
    if (resp.data.code !== 0) return false
    localStorage.setItem('light.accessToken', resp.data.data.accessToken)
    localStorage.setItem('light.refreshToken', resp.data.data.refreshToken)
    return true
  } catch {
    localStorage.removeItem('light.accessToken')
    localStorage.removeItem('light.refreshToken')
    return false
  }
}

async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const body = (await promise).data
  if (body.code !== 0) {
    ElMessage.error(body.message || '请求失败')
    throw new ApiError(body.code, body.message)
  }
  return body.data
}

export const http = {
  get: <T>(url: string) => unwrap<T>(instance.get<ApiResponse<T>>(url)),
  post: <T>(url: string, payload?: unknown) => unwrap<T>(instance.post<ApiResponse<T>>(url, payload)),
  put: <T>(url: string, payload?: unknown) => unwrap<T>(instance.put<ApiResponse<T>>(url, payload)),
  delete: <T>(url: string) => unwrap<T>(instance.delete<ApiResponse<T>>(url)),
}

export default http
