import axios from 'axios'
import { ElMessage } from 'element-plus'

/** 后台管理端统一请求封装（与 /web 同构：统一返回体 + admin 令牌注入） */
export interface ApiResponse<T = unknown> {
  code: number
  message: string
  data: T
  traceId: string
}

export class ApiError extends Error {
  readonly code: number
  constructor(code: number, message: string) {
    super(message)
    this.code = code
  }
}

const instance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE ?? 'http://127.0.0.1:5080',
  timeout: 15000,
})

instance.interceptors.request.use((config) => {
  const token = localStorage.getItem('light.adminToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

instance.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('light.adminToken')
      window.location.assign('/login')
      return Promise.reject(error)
    }
    ElMessage.error('网络异常，请稍后再试')
    return Promise.reject(error)
  },
)

async function unwrap<T>(promise: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const body = (await promise).data
  if (body.code !== 0) {
    ElMessage.error(body.message || '操作失败')
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
