using Hanora.Application.Abstractions;
using Hanora.Application.Common;
using Hanora.Application.Users;
using Hanora.Domain.Constants;
using Hanora.Domain.Entities;
using Hanora.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hanora.Infrastructure.Services;

public sealed class UserService(
    HanoraDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IUserService
{
    public async Task<PagedResult<UserDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new UserDto(
                x.Id,
                x.Name,
                x.Email,
                x.Role.Name,
                x.IsActive,
                x.MustChangePassword,
                x.LastLoginAt,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserDto>(items, safePage, safePageSize, totalItems);
    }

    public async Task<ServiceResult<UserDto>> CreateAsync(
        CreateUserRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roleName = NormalizeRole(request.Role);
        if (roleName is null)
        {
            return ServiceResult<UserDto>.Failure(
                "invalid_role", "Role harus Owner atau Cashier.");
        }

        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return ServiceResult<UserDto>.Failure(
                "duplicate_email", "Email sudah digunakan.");
        }

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

        if (role is null)
        {
            return ServiceResult<UserDto>.Failure(
                "role_not_found", "Role tidak tersedia di database.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            Role = role,
            Name = request.Name.Trim(),
            Email = email,
            MustChangePassword = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);

        await auditWriter.WriteAsync(
            actorId,
            "user.created",
            "user",
            user.Id.ToString(),
            new { role = role.Name, email = user.Email },
            ipAddress,
            cancellationToken);

        return ServiceResult<UserDto>.Success(Map(user, role.Name));
    }

    public async Task<ServiceResult<UserDto>> UpdateAsync(
        Guid userId,
        UpdateUserRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserDto>.Failure("not_found", "User tidak ditemukan.");
        }

        var roleName = NormalizeRole(request.Role);
        if (roleName is null)
        {
            return ServiceResult<UserDto>.Failure(
                "invalid_role", "Role harus Owner atau Cashier.");
        }

        if (userId == actorId && roleName != RoleNames.Owner)
        {
            return ServiceResult<UserDto>.Failure(
                "cannot_change_own_role", "Owner tidak dapat menurunkan role dirinya sendiri.");
        }

        if (user.Role.Name == RoleNames.Owner && roleName != RoleNames.Owner)
        {
            var otherActiveOwners = await dbContext.Users.CountAsync(
                x => x.Id != userId &&
                     x.Role.Name == RoleNames.Owner &&
                     x.IsActive,
                cancellationToken);

            if (otherActiveOwners == 0)
            {
                return ServiceResult<UserDto>.Failure(
                    "last_owner", "Minimal satu owner aktif harus tetap ada.");
            }
        }

        var email = NormalizeEmail(request.Email);
        if (await dbContext.Users.AnyAsync(
                x => x.Id != userId && x.Email == email,
                cancellationToken))
        {
            return ServiceResult<UserDto>.Failure(
                "duplicate_email", "Email sudah digunakan.");
        }

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

        if (role is null)
        {
            return ServiceResult<UserDto>.Failure(
                "role_not_found", "Role tidak tersedia di database.");
        }

        user.Name = request.Name.Trim();
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
        user.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            "user.updated",
            "user",
            user.Id.ToString(),
            new { role = role.Name, email = user.Email },
            ipAddress,
            cancellationToken);

        return ServiceResult<UserDto>.Success(Map(user, role.Name));
    }

    public async Task<ServiceResult<UserDto>> UpdateStatusAsync(
        Guid userId,
        bool isActive,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserDto>.Failure("not_found", "User tidak ditemukan.");
        }

        if (!isActive && userId == actorId)
        {
            return ServiceResult<UserDto>.Failure(
                "cannot_deactivate_self", "Anda tidak dapat menonaktifkan akun sendiri.");
        }

        if (!isActive && user.Role.Name == RoleNames.Owner)
        {
            var otherActiveOwners = await dbContext.Users.CountAsync(
                x => x.Id != userId &&
                     x.Role.Name == RoleNames.Owner &&
                     x.IsActive,
                cancellationToken);

            if (otherActiveOwners == 0)
            {
                return ServiceResult<UserDto>.Failure(
                    "last_owner", "Minimal satu owner aktif harus tetap ada.");
            }
        }

        user.IsActive = isActive;
        user.UpdatedAt = timeProvider.GetUtcNow();

        await auditWriter.WriteAsync(
            actorId,
            isActive ? "user.activated" : "user.deactivated",
            "user",
            user.Id.ToString(),
            null,
            ipAddress,
            cancellationToken);

        return ServiceResult<UserDto>.Success(Map(user, user.Role.Name));
    }

    private static UserDto Map(User user, string roleName) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            roleName,
            user.IsActive,
            user.MustChangePassword,
            user.LastLoginAt,
            user.CreatedAt);

    private static string? NormalizeRole(string role) =>
        role.Trim().ToLowerInvariant() switch
        {
            "owner" => RoleNames.Owner,
            "cashier" => RoleNames.Cashier,
            _ => null
        };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
