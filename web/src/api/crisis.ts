import http from './request'

export interface Hotline {
  name: string
  number: string
  note: string | null
}

export interface CrisisHelp {
  disclaimer: string
  hotlines: Hotline[]
  advice: string
  helpUrl: string
}

export const fetchCrisisHelp = () => http.get<CrisisHelp>('/api/crisis/help')
