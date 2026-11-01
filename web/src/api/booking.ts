import http from './request'

export interface CounselorItem {
  id: number
  name: string
  title: string | null
  specialties: string | null
  avatarUrl: string | null
}

export interface SlotItem {
  id: number
  startAt: string
  endAt: string
}

export interface BookingItem {
  id: number
  counselorId: number
  counselorName: string
  startAt: string
  endAt: string
  status: string
  paymentStatus: string
  bookedAt: string
  cancelledAt: string | null
}

export interface NotificationItem {
  id: number
  type: string
  title: string
  body: string | null
  isRead: boolean
  createdAt: string
}

export const fetchCounselors = () => http.get<CounselorItem[]>('/api/counselors')

export const fetchCounselorSlots = (id: number) =>
  http.get<SlotItem[]>(`/api/counselors/${id}/slots`)

export const createBooking = (slotId: number) =>
  http.post<BookingItem>('/api/bookings', { slotId })

export const fetchMyBookings = () => http.get<BookingItem[]>('/api/bookings/my')

export const cancelBooking = (id: number, reason: string | null) =>
  http.post<{ done: boolean }>(`/api/bookings/${id}/cancel`, { reason })

export const fetchNotifications = () =>
  http.get<{ items: NotificationItem[] }>('/api/me/notifications')

export const markNotificationRead = (id: number) =>
  http.post<{ done: boolean }>(`/api/me/notifications/${id}/read`)
