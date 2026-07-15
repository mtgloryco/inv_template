using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryManagementSystem.Domain;
using InventoryManagementSystem.Infrastructure;

namespace InventoryManagementSystem.Services
{
    public class WorkflowApprovalService
    {
        private readonly DatabaseService _databaseService;
        private readonly PurchaseOrderService? _purchaseOrderService;
        private readonly AuditService? _auditService;

        public const decimal DefaultPoApprovalThreshold = 1000m;
        public const decimal DefaultDiscountApprovalThreshold = 50m;

        public WorkflowApprovalService(
            DatabaseService databaseService,
            PurchaseOrderService? purchaseOrderService = null,
            AuditService? auditService = null)
        {
            _databaseService = databaseService;
            _purchaseOrderService = purchaseOrderService;
            _auditService = auditService;
        }

        public async Task<List<ApprovalRequest>> GetPendingApprovalsAsync()
        {
            return await _databaseService.Connection.Table<ApprovalRequest>()
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<ApprovalRequest> SubmitApprovalRequestAsync(
            string requestType,
            string referenceType,
            int referenceId,
            decimal amount,
            string requestedBy,
            string notes = "")
        {
            var existing = await _databaseService.Connection.Table<ApprovalRequest>()
                .FirstOrDefaultAsync(r =>
                    r.RequestType == requestType &&
                    r.ReferenceType == referenceType &&
                    r.ReferenceId == referenceId &&
                    r.Status == "Pending");

            if (existing != null)
            {
                return existing;
            }

            var request = new ApprovalRequest
            {
                RequestType = requestType,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Amount = amount,
                Status = "Pending",
                RequestedByUsername = requestedBy,
                RequestNotes = notes,
                CreatedAt = DateTime.UtcNow
            };

            await _databaseService.Connection.InsertAsync(request);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(requestedBy, "Submit", "ApprovalRequest", request.Id, request);
            }

            return request;
        }

        public async Task<ApprovalRequest> ApproveAsync(int requestId, string reviewerUsername, string reviewNotes = "")
        {
            var request = await _databaseService.Connection.FindAsync<ApprovalRequest>(requestId)
                          ?? throw new InvalidOperationException("Approval request not found.");

            if (request.Status != "Pending")
            {
                throw new InvalidOperationException($"Request is already {request.Status}.");
            }

            request.Status = "Approved";
            request.ReviewedByUsername = reviewerUsername;
            request.ReviewNotes = reviewNotes;
            request.ReviewedAt = DateTime.UtcNow;
            await _databaseService.Connection.UpdateAsync(request);

            await ApplyApprovalSideEffectsAsync(request, reviewerUsername);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(reviewerUsername, "Approve", "ApprovalRequest", request.Id, request);
            }

            return request;
        }

        public async Task<ApprovalRequest> RejectAsync(int requestId, string reviewerUsername, string reviewNotes = "")
        {
            var request = await _databaseService.Connection.FindAsync<ApprovalRequest>(requestId)
                          ?? throw new InvalidOperationException("Approval request not found.");

            if (request.Status != "Pending")
            {
                throw new InvalidOperationException($"Request is already {request.Status}.");
            }

            request.Status = "Rejected";
            request.ReviewedByUsername = reviewerUsername;
            request.ReviewNotes = reviewNotes;
            request.ReviewedAt = DateTime.UtcNow;
            await _databaseService.Connection.UpdateAsync(request);

            if (_auditService != null)
            {
                await _auditService.LogActionAsync(reviewerUsername, "Reject", "ApprovalRequest", request.Id, request);
            }

            return request;
        }

        public async Task<bool> RequiresPurchaseOrderApprovalAsync(decimal totalAmount, decimal? threshold = null)
        {
            return totalAmount >= (threshold ?? DefaultPoApprovalThreshold);
        }

        public async Task<ApprovalRequest?> RequestPurchaseOrderApprovalAsync(int purchaseOrderId, string requestedBy)
        {
            var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(purchaseOrderId);
            if (po == null)
            {
                return null;
            }

            po.Status = "PendingApproval";
            await _databaseService.Connection.UpdateAsync(po);

            return await SubmitApprovalRequestAsync(
                "PurchaseOrder", "PurchaseOrder", purchaseOrderId, po.TotalAmount, requestedBy,
                $"PO {po.PONumber} requires manager approval.");
        }

        public async Task<ApprovalRequest> RequestDiscountApprovalAsync(
            int salesOrderId, decimal discountAmount, string requestedBy, string notes = "")
        {
            return await SubmitApprovalRequestAsync(
                "Discount", "SalesOrder", salesOrderId, discountAmount, requestedBy, notes);
        }

        public async Task<ApprovalRequest> RequestWriteOffApprovalAsync(
            int productId, decimal writeOffValue, string requestedBy, string notes = "")
        {
            return await SubmitApprovalRequestAsync(
                "WriteOff", "Product", productId, writeOffValue, requestedBy, notes);
        }

        private async Task ApplyApprovalSideEffectsAsync(ApprovalRequest request, string reviewerUsername)
        {
            if (request.RequestType == "PurchaseOrder" && request.ReferenceType == "PurchaseOrder")
            {
                var po = await _databaseService.Connection.FindAsync<PurchaseOrder>(request.ReferenceId);
                if (po != null && po.Status == "PendingApproval")
                {
                    po.Status = "Approved";
                    po.ApprovedByUsername = reviewerUsername;
                    await _databaseService.Connection.UpdateAsync(po);
                }
            }
        }
    }
}
