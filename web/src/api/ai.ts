import http from './request'

export interface AiSessionItem {
  id: number
  title: string | null
  status: string
  startedAt: string
}

export interface AiMessageItem {
  id: number
  role: string
  content: string
  createdAt: string
}

export const createAiSession = () =>
  http.post<{ sessionId: number; disclaimer: string }>('/api/ai/sessions')

export const sendAiMessage = (sessionId: number, content: string) =>
  http.post<{ sessionId: number; messageId: number; reply: string }>(
    `/api/ai/sessions/${sessionId}/messages`, { content })

export const fetchAiMessages = (sessionId: number) =>
  http.get<{ sessionId: number; messages: AiMessageItem[]; disclaimer: string }>(
    `/api/ai/sessions/${sessionId}/messages`)
