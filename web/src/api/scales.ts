import http from './request'

export interface ScaleListItem {
  code: string
  name: string
  description: string | null
  questionCount: number
  estimatedMinutes: number
  sourceName: string | null
  sourceVerified: boolean
}

export interface ScaleOption {
  value: number
  label: string
}

export interface ScaleQuestion {
  no: number
  title: string
  options: ScaleOption[]
}

/** 对外投影：不含计分/分级/预警规则（防作弊） */
export interface ScaleDetail {
  code: string
  name: string
  description: string | null
  instructions: string | null
  sourceName: string | null
  sourceVerified: boolean
  disclaimer: string | null
  questions: ScaleQuestion[]
}

export interface ScaleAnswer {
  no: number
  value: number
}

export interface SubmissionCrisis {
  helpUrl: string
}

export interface SubmissionResult {
  submissionPublicId: string
  totalScore: number
  levelCode: string
  levelLabel: string
  disclaimer: string
  crisis: SubmissionCrisis | null
}

export const fetchScaleList = () => http.get<ScaleListItem[]>('/api/scales')

export const fetchScaleDetail = (code: string) => http.get<ScaleDetail>(`/api/scales/${code}`)

export const submitScaleAnswers = (scaleCode: string, answers: ScaleAnswer[]) =>
  http.post<SubmissionResult>('/api/scale-submissions', { scaleCode, answers })

export const fetchSubmissionResult = (publicId: string) =>
  http.get<SubmissionResult>(`/api/scale-submissions/${publicId}`)
