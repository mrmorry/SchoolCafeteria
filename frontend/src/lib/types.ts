// Mirrors the backend DTOs (SchoolCafeteria.Application.DTOs). Kept intentionally minimal —
// only the fields the UI actually renders — rather than a generated 1:1 client.

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
}

export interface LoginResult {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  user: UserProfile;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface StudentDto {
  id: string;
  studentCode: string;
  firstName: string;
  lastName: string;
  status: 'Active' | 'Inactive' | 'Suspended' | 'Graduated';
  schoolLevelId?: string;
  schoolLevelName?: string;
  schoolSectionId?: string;
  schoolSectionName?: string;
  studentEmail?: string;
  buyerId: string;
  walletId: string;
  walletBalance: number;
  hasRfid: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface EmployeeDto {
  id: string;
  employeeCode: string;
  fullName: string;
  email: string;
  employeeType: string;
  status: string;
  buyerId: string;
  walletId: string;
  walletBalance: number;
  hasRfid: boolean;
}

export interface WalletDto {
  id: string;
  buyerId: string;
  buyerName: string;
  currency: string;
  balance: number;
  heldBalance: number;
  status: 'Active' | 'Blocked' | 'Closed';
  maxBalance?: number;
  lowBalanceThreshold?: number;
}

export interface WalletTransactionDto {
  id: string;
  transactionNumber: string;
  type: string;
  status: string;
  channel: string;
  paymentMethod?: string;
  amount: number;
  balanceBefore: number;
  balanceAfter: number;
  performedByUserId: string;
  occurredAtUtc: string;
  comment?: string;
  reason?: string;
  externalReference?: string;
}

export interface ProductDto {
  id: string;
  code: string;
  barCode?: string;
  name: string;
  description?: string;
  categoryId: string;
  categoryName: string;
  unitOfMeasure: string;
  cost: number;
  basePrice: number;
  taxRate: number;
  status: string;
  availableForSale: boolean;
  trackInventory: boolean;
  minStockLevel: number;
  reorderLevel: number;
  allergens?: string;
  stockOnHand?: number;
}

export interface ProductCategoryDto {
  id: string;
  name: string;
  description?: string;
}

export interface InventoryBalanceDto {
  warehouseId: string;
  warehouseName: string;
  productId: string;
  productName: string;
  productCode: string;
  quantityOnHand: number;
  minStockLevel: number;
  isLow: boolean;
}

export interface RfidLookupResult {
  buyerId: string;
  buyerName: string;
  buyerType: string;
  walletId: string;
  walletBalance: number;
  walletStatus: string;
  rfidMaskedValue: string;
  allowedToPurchase: boolean;
  blockReason?: string;
}

export interface PointOfSaleDto {
  id: string;
  name: string;
  location?: string;
  isActive: boolean;
  registers: { id: string; name: string; isActive: boolean }[];
}

export interface ShiftDto {
  id: string;
  registerId: string;
  registerName: string;
  operatorUserId: string;
  status: 'Open' | 'Closed';
  openingFloat: number;
  closingCounted?: number;
  expectedCash?: number;
  cashDifference?: number;
  openedAtUtc: string;
  closedAtUtc?: string;
  totalSales: number;
  totalRecharges: number;
}

export interface SaleLineDto {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  discountAmount: number;
  lineTotal: number;
}

export interface SaleDto {
  id: string;
  saleNumber: string;
  buyerId: string;
  buyerName: string;
  subtotal: number;
  taxTotal: number;
  discountTotal: number;
  total: number;
  status: string;
  operatorUserId: string;
  occurredAtUtc: string;
  lines: SaleLineDto[];
  balanceAfter: number;
}

export interface DashboardSummaryDto {
  todaySales: number;
  todayRecharges: number;
  todayTransactions: number;
  lowStockProducts: number;
  activeWallets: number;
  totalWalletBalance: number;
}

export interface GuardianDto {
  id: string;
  fullName: string;
  email: string;
  phone?: string;
  students: {
    studentId: string;
    studentFullName: string;
    relationship: string;
    isPrimary: boolean;
    canRecharge: boolean;
    canViewHistory: boolean;
    canManageRfid: boolean;
    canConfigureAlerts: boolean;
  }[];
}

export interface ImportJobDto {
  id: string;
  fileName: string;
  status: string;
  totalRows: number;
  validRows: number;
  errorRows: number;
  duplicateRows: number;
  importedRows: number;
  createdAtUtc: string;
  completedAtUtc?: string;
}

export interface ImportPreviewRowDto {
  rowNumber: number;
  naturalKey: string;
  status: string;
  errorMessage?: string;
  rawDataJson: string;
}

export interface AuditLogDto {
  id: string;
  userId?: string;
  action: string;
  entityName: string;
  entityId?: string;
  occurredAtUtc: string;
  reason?: string;
  correlationId?: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  correlationId?: string;
}
