import http from './request'

export interface DiaryListItem {
  id: number
  entryDate: string
  moodScore: number
  tags: string[]
  hasCrisis: boolean
  createdAt: string
}

export interface DiaryDetail {
  id: number
  entryDate: string
  moodScore: number
  tags: string[]
  content: string
  hasCrisis: boolean
  createdAt: string
  updatedAt: string
}

export interface DiaryTrendItem {
  date: string
  moodScore: number
}

export interface DiaryReminder {
  enabled: boolean
  remindTime: string
  daysOfWeek: string
}

export interface DiaryUpsertPayload {
  entryDate: string
  moodScore: number
  content: string
  tags: string[]
}

export const fetchDiaries = (page = 1, pageSize = 20) =>
  http.get<{ items: DiaryListItem[]; total: number }>(`/api/diaries?page=${page}&pageSize=${pageSize}`)

export const fetchDiary = (id: number | string) => http.get<DiaryDetail>(`/api/diaries/${id}`)

export const createDiary = (payload: DiaryUpsertPayload) => http.post<DiaryDetail>('/api/diaries', payload)

export const updateDiary = (id: number | string, payload: DiaryUpsertPayload) =>
  http.put<DiaryDetail>(`/api/diaries/${id}`, payload)

export const deleteDiary = (id: number | string) => http.delete<{ done: boolean }>(`/api/diaries/${id}`)

export const fetchTrend = (days = 30) => http.get<DiaryTrendItem[]>(`/api/diaries/trend?days=${days}`)

export const fetchReminder = () => http.get<DiaryReminder>('/api/diaries/reminder')

export const saveReminder = (payload: DiaryReminder) => http.put<{ done: boolean }>('/api/diaries/reminder', payload)
