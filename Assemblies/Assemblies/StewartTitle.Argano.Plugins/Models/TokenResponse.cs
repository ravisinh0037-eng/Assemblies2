// <copyright file="TokenResponse.cs" company="Stewart">
// Copyright 2025 Stewart
// </copyright>

namespace StewartTitle.Argano.Plugins.Models
{
    /// <summary>
    /// This class models the Token object used for authentication purposes.
    /// </summary>
    public class TokenResponse
    {
        public string token_type { get; set; }

        public string access_token { get; set; }

        public int expires_in { get; set; }

        public int ext_expires_in { get; set; }
    }
}
