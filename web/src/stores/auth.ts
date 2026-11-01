import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import * as authApi from '../api/auth'
import type { UserBrief } from '../api/auth'

const ACCESS_KEY = 'light.accessToken'
const REFRESH_KEY = 'light.refreshToken'
const USER_KEY = 'light.user'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string>(localStorage.getItem(ACCESS_KEY) ?? '')
  const refreshTokenValue = ref<string>(localStorage.getItem(REFRESH_KEY) ?? '')
  const user = ref<UserBrief | null>(safeParse(localStorage.getItem(USER_KEY)))

  const isLoggedIn = computed(() => accessToken.value.length > 0)
  const needPrivacyConsent = computed(() =>
    isLoggedIn.value && user.value !== null && user.value.privacyConsentAt === null)

  function safeParse(raw: string | null): UserBrief | null {
    if (!raw) return null
    try {
      return JSON.parse(raw) as UserBrief
    } catch {
      return null
    }
  }

  function setSession(pair: authApi.TokenPair) {
    accessToken.value = pair.accessToken
    refreshTokenValue.value = pair.refreshToken
    user.value = pair.user
    localStorage.setItem(ACCESS_KEY, pair.accessToken)
    localStorage.setItem(REFRESH_KEY, pair.refreshToken)
    localStorage.setItem(USER_KEY, JSON.stringify(pair.user))
  }

  async function doLogin(account: string, password: string) {
    setSession(await authApi.login(account, password))
  }

  async function doRegister(account: string, password: string, nickname: string | null, consent: boolean) {
    setSession(await authApi.register(account, password, nickname, consent))
  }

  async function doSmsLogin(phone: string, code: string) {
    setSession(await authApi.smsLogin(phone, code))
  }

  async function doLogout() {
    try {
      if (refreshTokenValue.value) {
        await authApi.logout(refreshTokenValue.value)
      }
    } finally {
      clear()
    }
  }

  async function doRefresh(): Promise<boolean> {
    if (!refreshTokenValue.value) return false
    try {
      setSession(await authApi.refresh(refreshTokenValue.value))
      return true
    } catch {
      clear()
      return false
    }
  }

  async function acceptPrivacyConsent() {
    await authApi.recordPrivacyConsent()
    if (user.value) {
      user.value = { ...user.value, privacyConsentAt: new Date().toISOString() }
      localStorage.setItem(USER_KEY, JSON.stringify(user.value))
    }
  }

  function clear() {
    accessToken.value = ''
    refreshTokenValue.value = ''
    user.value = null
    localStorage.removeItem(ACCESS_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(USER_KEY)
  }

  return {
    accessToken, refreshTokenValue, user,
    isLoggedIn, needPrivacyConsent,
    setSession, doLogin, doRegister, doSmsLogin, doLogout, doRefresh, acceptPrivacyConsent, clear,
  }
})
