import http from './request'

export interface ArticleCategory {
  id: number
  name: string
  slug: string
}

export interface ArticleListItem {
  slug: string
  title: string
  summary: string | null
  publishedAt: string | null
}

export interface ArticleDetail {
  slug: string
  title: string
  content: string
  sourceInfo: string
  reviewerName: string
  publishedAt: string | null
  updatedAt: string
  disclaimer: string
}

export const fetchArticleCategories = () => http.get<ArticleCategory[]>('/api/articles/categories')

export const fetchArticles = (categoryId: number | null, page = 1) => {
  const qs = categoryId ? `&categoryId=${categoryId}` : ''
  return http.get<{ items: ArticleListItem[]; page: number; pageSize: number }>(
    `/api/articles?page=${page}${qs}`,
  )
}

export const fetchArticleDetail = (slug: string) => http.get<ArticleDetail>(`/api/articles/${slug}`)

/** 社区 SSO：持令牌换取 Discourse 回跳地址（SSO 处理页用） */
export const exchangeCommunitySso = (sso: string, sig: string) =>
  http.post<{ redirectUrl: string }>('/api/community/sso-url', { sso, sig })
