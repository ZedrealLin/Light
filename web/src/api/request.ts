import axios from 'axios'
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

instance.interceptors.response.use(
  (response) => response,
  (error) => {
    ElMessage.error('网络异常，请稍后再试')
    return Promise.reject(error)
  },
)

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
}

export default http
