import http from './request'

export interface AdminAlertItem {
  id: number
  source: string
  status: string
  detailJson: string | null
  userId: number | null
  externalRef: string | null
  createdAt: string
  handledAt: string | null
  handleNote: string | null
}

export interface AdminUserItem {
  id: number
  publicId: string
  nickname: string | null
  phoneMasked: string | null
  emailMasked: string | null
  status: string
  createdAt: string
}

export const adminLogin = (username: string, password: string) =>
  http.post<{ accessToken: string; displayName: string }>('/api/admin/auth/login', { username, password })

export const fetchAlerts = (status: string | null, page = 1) => {
  const qs = status ? `&status=${status}` : ''
  return http.get<{ items: AdminAlertItem[]; page: number; pageSize: number }>(
    `/api/admin/crisis-alerts?page=${page}${qs}`,
  )
}

export const handleAlert = (id: number, action: string, note: string | null) =>
  http.post<{ done: boolean }>(`/api/admin/crisis-alerts/${id}/handle`, { action, note })

export const fetchUsers = (query: string | null, page = 1) => {
  const qs = query ? `&query=${encodeURIComponent(query)}` : ''
  return http.get<{ items: AdminUserItem[] }>(`/api/admin/users?page=${page}${qs}`)
}

export const disableUser = (id: number) => http.post<{ done: boolean }>(`/api/admin/users/${id}/disable`)

export const enableUser = (id: number) => http.post<{ done: boolean }>(`/api/admin/users/${id}/enable`)

export interface ScaleVersion {
  id: number
  code: string
  name: string
  version: number
  isActive: boolean
  questionCount: number
}

export const fetchScales = () => http.get<ScaleVersion[]>('/api/scales')

export const setScaleActive = (id: number, isActive: boolean) =>
  http.post<{ done: boolean }>(`/api/admin/scales/${id}/active`, { isActive })

export const fetchKeywords = (onlyActive = false) =>
  http.get<string[]>(`/api/admin/crisis-keywords?onlyActive=${onlyActive}`)

export const addKeyword = (keyword: string) =>
  http.post<{ done: boolean }>('/api/admin/crisis-keywords', { keyword })

export const toggleKeyword = (keyword: string, isActive: boolean) =>
  http.post<{ done: boolean }>('/api/admin/crisis-keywords/toggle', { keyword, isActive })

// ---------- 文章审核 ----------

export interface AdminArticleItem {
  id: number
  title: string
  status: string
  sourceInfo: string
  reviewNote: string | null
  publishedAt: string | null
  updatedAt: string
}

export const fetchArticles = (status: string | null, page = 1) => {
  const qs = status ? `&status=${status}` : ''
  return http.get<{ items: AdminArticleItem[] }>(`/api/admin/articles?page=${page}${qs}`)
}

export const createArticle = (payload: {
  categoryId: number | null
  title: string
  slug: string | null
  summary: string | null
  content: string
  sourceInfo: string
}) => http.post<{ id: number }>('/api/admin/articles', payload)

export const submitArticle = (id: number) => http.post<{ done: boolean }>(`/api/admin/articles/${id}/submit`)

export const reviewArticle = (id: number, approve: boolean, note: string | null) =>
  http.post<{ done: boolean }>(`/api/admin/articles/${id}/review`, { approve, note })

export const offlineArticle = (id: number) => http.post<{ done: boolean }>(`/api/admin/articles/${id}/offline`)
