using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using XeroDotnetSampleApp.Models;

namespace XeroDotnetSampleApp.Services
{
    public class DatabaseService
    {
        private readonly UserContext _userContext;

        public DatabaseService(UserContext userContext)
        {
            _userContext = userContext;
        }


        /// Reads all referral users from the database
        public async Task<List<SignUpWithXeroUser>> ReadAllData()
        {
            return await _userContext.SignUpWithXeroUsers.ToListAsync();
        }

        
        // Finds a user in the database by TenantId
        public async Task<SignUpWithXeroUser> FindUserByTenantId(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
            }

            return await _userContext.SignUpWithXeroUsers
                .FirstOrDefaultAsync(user => user.TenantId == tenantId);
        }

        // Finds a user in the database by SubscriptionId
        public async Task<SignUpWithXeroUser> FindUserBySubscriptionId(string subscriptionId)
        {
            if (string.IsNullOrEmpty(subscriptionId))
            {
                throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(subscriptionId));
            }

            return await _userContext.SignUpWithXeroUsers
                .FirstOrDefaultAsync(user => user.SubscriptionId == subscriptionId);
        }

        /// Registers or updates a user in the database
        public async Task RegisterUserToDb(SignUpWithXeroUser user)
        {
            if (user == null || string.IsNullOrEmpty(user.XeroUserId))
            {
                throw new ArgumentException("Invalid user data.");
            }

            var existingUser = await _userContext.SignUpWithXeroUsers.FindAsync(user.XeroUserId);

            if (existingUser != null)
            {
                // Update existing user
                existingUser.Email = user.Email;
                existingUser.GivenName = user.GivenName;
                existingUser.FamilyName = user.FamilyName;
                existingUser.TenantId = user.TenantId;
                existingUser.TenantName = user.TenantName;
                existingUser.AuthEventId = user.AuthEventId;
                existingUser.ConnectionCreatedDateUtc = user.ConnectionCreatedDateUtc;
                existingUser.TenantShortCode = user.TenantShortCode;
                existingUser.TenantCountryCode = user.TenantCountryCode;
                existingUser.AccountCreatedDateTime = user.AccountCreatedDateTime;
                existingUser.SubscriptionId = user.SubscriptionId;
                existingUser.SubscriptionPlan = user.SubscriptionPlan;

                _userContext.SignUpWithXeroUsers.Update(existingUser);
            }
            else
            {
                // Create new user
                _userContext.SignUpWithXeroUsers.Add(user);
            }

            await _userContext.SaveChangesAsync();
            SaveAndUpdateDB();
        }

        public void SaveAndUpdateDB()
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=SignUpWithXeroUsers.db"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA wal_checkpoint(FULL);";
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
