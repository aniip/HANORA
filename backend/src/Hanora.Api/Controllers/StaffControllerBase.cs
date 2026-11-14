using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hanora.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Hanora.Api.Controllers;

public abstract class StaffControllerBase : ControllerBase
{
    protected Guid? GetUserId()
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    protected string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    protected IActionResult UnauthorizedProblem() =>
        Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Tidak terautentikasi",
            detail: "Token tidak valid.");

    protected IActionResult RequestProblem(
        string? detail,
        string code = "validation") =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Permintaan tidak valid",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code
            });

    protected IActionResult FailureProblem<T>(ServiceResult<T> result)
    {
        var statusCode = result.ErrorCode switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "qr_not_found" => StatusCodes.Status404NotFound,
            "category_not_found" => StatusCodes.Status400BadRequest,
            "duplicate_email" => StatusCodes.Status409Conflict,
            "duplicate_slug" => StatusCodes.Status409Conflict,
            "duplicate_table" => StatusCodes.Status409Conflict,
            "category_in_use" => StatusCodes.Status409Conflict,
            "table_in_use" => StatusCodes.Status409Conflict,
            "table_session_invalid" => StatusCodes.Status409Conflict,
            "product_unavailable" => StatusCodes.Status409Conflict,
            "idempotency_conflict" => StatusCodes.Status409Conflict,
            "invalid_status" => StatusCodes.Status409Conflict,
            "invalid_status_transition" => StatusCodes.Status409Conflict,
            "payment_not_payable" => StatusCodes.Status409Conflict,
            "order_not_cancellable" => StatusCodes.Status409Conflict,
            "qris_disabled" => StatusCodes.Status409Conflict,
            "simulation_disabled" => StatusCodes.Status409Conflict,
            "income_from_order" => StatusCodes.Status409Conflict,
            "provider_error" => StatusCodes.Status502BadGateway,
            "archived" => StatusCodes.Status409Conflict,
            "cannot_change_own_role" => StatusCodes.Status409Conflict,
            "cannot_deactivate_self" => StatusCodes.Status409Conflict,
            "last_owner" => StatusCodes.Status409Conflict,
            "file_too_large" => StatusCodes.Status413PayloadTooLarge,
            "empty_file" => StatusCodes.Status400BadRequest,
            "invalid_image" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            statusCode: statusCode,
            title: "Operasi gagal",
            detail: result.ErrorMessage,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = result.ErrorCode
            });
    }
}
