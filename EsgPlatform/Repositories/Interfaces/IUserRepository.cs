using EsgPlatform.Models;

namespace EsgPlatform.Repositories.Interfaces;

/// <summary>使用者資料存取介面</summary>
public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByIdAsync(int id);
    Task<IEnumerable<User>> GetAllAsync();

    /// <summary>建立新帳號</summary>
    Task<int> CreateAsync(User user);

    /// <summary>更新帳號資料（Email、RoleId、IsActive）</summary>
    Task UpdateAsync(User user);

    /// <summary>更新密碼雜湊</summary>
    Task UpdatePasswordHashAsync(int id, string passwordHash);

    /// <summary>刪除帳號</summary>
    Task DeleteAsync(int id);

    /// <summary>檢查使用者名稱是否已存在</summary>
    Task<bool> UsernameExistsAsync(string username, int? excludeId = null);

    /// <summary>檢查 Email 是否已存在</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);
}
