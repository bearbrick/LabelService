/**
 * 账号、用户和角色的管理接口（docs/admin-api.md 的 /login、/me、/users、/roles）。
 */
import { api } from './http.ts'

/**
 * 当前登录的用户（GET /me）。
 */
export interface Me {
  /**
   * 用户 ID。
   */
  id: number
  /**
   * 登录账号。
   */
  account: string
  /**
   * 姓名。
   */
  name: string
  /**
   * 手机号；没填时为空字符串。
   */
  phone: string
  /**
   * 角色。
   */
  role: {
    /**
     * 角色 ID。
     */
    id: number
    /**
     * 角色名称。
     */
    name: string
  }
  /**
   * 拥有的权限点，如 tpl.edit。
   */
  permissions: string[]
  /**
   * 能访问全部业务分组。
   */
  allBusinesses: boolean
  /**
   * 能访问的业务分组 ID（allBusinesses 为 true 时不看）。
   */
  businessIds: number[]
  /**
   * 首次登录或重置密码后必须先改密码。
   */
  mustChangePassword: boolean
  /**
   * 上次登录时间；从没登录过时为 null。
   */
  lastLoginAt: string | null
}

/**
 * 权限点目录的一项（GET /permissions）。
 */
export interface PermissionInfo {
  /**
   * 权限点，如 tpl.publish。
   */
  key: string
  /**
   * 显示名称，如“发布模板”。
   */
  label: string
  /**
   * 所属分组，角色编辑页按它分栏。
   */
  group: string
}

/**
 * 用户列表的一项（GET /users）。
 */
export interface User {
  /**
   * 用户 ID。
   */
  id: number
  /**
   * 登录账号。
   */
  account: string
  /**
   * 姓名。
   */
  name: string
  /**
   * 手机号。
   */
  phone: string
  /**
   * 角色 ID。
   */
  roleId: number
  /**
   * 能访问全部业务分组。
   */
  allBusinesses: boolean
  /**
   * 能访问的业务分组 ID。
   */
  businessIds: number[]
  /**
   * 启用或停用。
   */
  status: 'active' | 'disabled'
  /**
   * 下次登录必须先改密码。
   */
  mustChangePassword: boolean
  /**
   * 上次登录时间；从没登录过时为 null。
   */
  lastLoginAt: string | null
  /**
   * 创建时间。
   */
  createdAt: string
}

/**
 * 角色列表的一项（GET /roles）。
 */
export interface Role {
  /**
   * 角色 ID。
   */
  id: number
  /**
   * 角色名称。
   */
  name: string
  /**
   * 说明。
   */
  description: string
  /**
   * 拥有全部权限（系统管理员）。
   */
  allPermissions: boolean
  /**
   * 拥有的权限点。
   */
  permissions: string[]
  /**
   * 内置角色（不能删）。
   */
  builtin: boolean
  /**
   * 使用这个角色的用户人数。
   */
  users: number
}

/**
 * 新建或修改用户的请求体。
 */
export interface UserInput {
  /**
   * 登录账号；只在新建时传，账号不能改。
   */
  account?: string
  /**
   * 姓名。
   */
  name: string
  /**
   * 角色 ID（不能改自己的角色）。
   */
  roleId: number
  /**
   * 能访问全部业务分组。
   */
  allBusinesses: boolean
  /**
   * 能访问的业务分组 ID。
   */
  businessIds: number[]
}

/**
 * 新建或修改角色的请求体：直接给权限点，或者从已有角色复制。
 */
export interface RoleInput {
  /**
   * 角色名称。
   */
  name: string
  /**
   * 说明。
   */
  description: string
  /**
   * 权限点；从已有角色复制时不传。
   */
  permissions?: string[]
  /**
   * 复制权限的来源角色 ID；只在新建时用。
   */
  copyFromRoleId?: number
}

/**
 * 登录。
 * @param account 账号。
 * @param password 密码。
 * @param remember 记住登录（令牌有效期更长）。
 * @returns 令牌、过期时间和当前用户。
 */
export const login = (account: string, password: string, remember: boolean) =>
  api<{ token: string; expiresAt: string; me: Me }>('POST', '/login', { account, password, remember })

/**
 * 退出登录（服务端记一条操作日志）。
 * @returns 请求完成时结束。
 */
export const logout = () => api<void>('POST', '/logout')

/**
 * 取当前登录的用户。
 * @returns 当前用户。
 */
export const getMe = () => api<Me>('GET', '/me')

/**
 * 修改自己的姓名和手机号。
 * @param name 姓名。
 * @param phone 手机号。
 * @returns 修改后的当前用户。
 */
export const updateProfile = (name: string, phone: string) => api<Me>('PUT', '/me', { name, phone })

/**
 * 修改自己的密码（新密码至少 8 位，同时有字母和数字）。
 * @param oldPassword 原密码。
 * @param newPassword 新密码。
 * @returns 请求完成时结束。
 */
export const changePassword = (oldPassword: string, newPassword: string) => api<void>('POST', '/me/password', { oldPassword, newPassword })

/**
 * 取权限点目录。
 * @returns 全部权限点。
 */
export const listPermissions = () => api<PermissionInfo[]>('GET', '/permissions')

/**
 * 取用户列表。
 * @returns 全部用户。
 */
export const listUsers = () => api<User[]>('GET', '/users')

/**
 * 新建用户，服务端生成临时密码（首次登录必须改）。
 * @param input 用户信息。
 * @returns 新用户和临时密码（只返回这一次）。
 */
export const createUser = (input: UserInput) => api<{ user: User; temporaryPassword: string }>('POST', '/users', input)

/**
 * 修改用户。
 * @param id 用户 ID。
 * @param input 用户信息。
 * @returns 修改后的用户。
 */
export const updateUser = (id: number, input: UserInput) => api<User>('PUT', `/users/${id}`, input)

/**
 * 启用或停用用户（不能停用自己）。
 * @param id 用户 ID。
 * @param enabled true 启用，false 停用。
 * @returns 修改后的用户。
 */
export const setUserStatus = (id: number, enabled: boolean) => api<User>('POST', `/users/${id}/status`, { enabled })

/**
 * 重置用户密码，同时解锁。
 * @param id 用户 ID。
 * @returns 新的临时密码（只返回这一次）。
 */
export const resetPassword = (id: number) => api<{ temporaryPassword: string }>('POST', `/users/${id}/reset-password`)

/**
 * 取角色列表。
 * @returns 全部角色。
 */
export const listRoles = () => api<Role[]>('GET', '/roles')

/**
 * 新建角色。
 * @param input 角色信息。
 * @returns 新角色。
 */
export const createRole = (input: RoleInput) => api<Role>('POST', '/roles', input)

/**
 * 修改角色（系统管理员角色不能改）。
 * @param id 角色 ID。
 * @param input 角色信息。
 * @returns 修改后的角色。
 */
export const updateRole = (id: number, input: RoleInput) => api<Role>('PUT', `/roles/${id}`, input)

/**
 * 删除角色（内置角色、有人在用的角色不能删）。
 * @param id 角色 ID。
 * @returns 请求完成时结束。
 */
export const deleteRole = (id: number) => api<void>('DELETE', `/roles/${id}`)
