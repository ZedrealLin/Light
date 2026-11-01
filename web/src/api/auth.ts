import http from './request'

export interface UserBrief {
  publicId: string
  nickname: string | null
  phoneMasked: string | null
  emailMasked: string | null
  privacyConsentAt: string | null
}

export interface TokenPair {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  user: UserBrief
}

export const register = (account: string, password: string, nickname: string | null, privacyConsent: boolean) =>
  http.post<TokenPair>('/api/auth/register', { account, password, nickname, privacyConsent })

export const login = (account: string, password: string) =>
  http.post<TokenPair>('/api/auth/login', { account, password })

export const sendSmsCode = (phone: string) =>
  http.post<{ sent: boolean; mockCode?: string }>('/api/auth/sms-code', { phone })

export const smsLogin = (phone: string, code: string) =>
  http.post<TokenPair>('/api/auth/login/sms', { phone, code })

export const refresh = (refreshToken: string) =>
  http.post<TokenPair>('/api/auth/refresh', { refreshToken })

export const logout = (refreshToken: string) =>
  http.post<{ done: boolean }>('/api/auth/logout', { refreshToken })

export const fetchMe = () => http.get<UserBrief>('/api/me')

export const recordPrivacyConsent = () => http.post<{ done: boolean }>('/api/me/privacy-consent')
