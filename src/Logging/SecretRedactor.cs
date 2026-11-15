using System;
using System.Text.RegularExpressions;

namespace NetworkSharing.Logging
{
    /// <summary>
    /// Enterprise-grade secret redaction engine.
    /// Scans log strings, exceptions, and diagnostic outputs for passwords, keys, and credentials,
    /// replacing sensitive substrings with [REDACTED].
    /// </summary>
    public static class SecretRedactor
    {
        private static readonly Regex[] RedactionRules = new Regex[]
        {
            // Password key-value pairs (e.g. password=XYZ, pwd: "XYZ", "password": "XYZ")
            new Regex(@"(?i)(password|passwd|pwd|secret|token|credential|auth)\s*[:=]\s*[""']?([^""';\s]+)[""']?", RegexOptions.Compiled),
            
            // net.exe or command line user creation passwords: net user username password /add
            new Regex(@"(?i)(net\s+user\s+[""']?[^""'\s]+[""']?\s+)([""']?[^""'\s/]+[""']?)(\s+/add)", RegexOptions.Compiled),
            
            // UNC credentials in URI format: smb://user:password@host
            new Regex(@"(?i)(smb://[^:]+:)([^@]+)(@)", RegexOptions.Compiled),

            // Base64 Authorization / Bearer tokens
            new Regex(@"(?i)(bearer|basic)\s+([A-Za-z0-9+/=]{16,})", RegexOptions.Compiled)
        };

        /// <summary>
        /// Redacts any sensitive data from the provided message.
        /// </summary>
        public static string Redact(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            string sanitized = message;

            // Rule 1: Key-value pattern
            sanitized = RedactionRules[0].Replace(sanitized, "$1=[REDACTED]");

            // Rule 2: net user command line password
            sanitized = RedactionRules[1].Replace(sanitized, "$1[REDACTED]$3");

            // Rule 3: URI credentials
            sanitized = RedactionRules[2].Replace(sanitized, "$1[REDACTED]$3");

            // Rule 4: Bearer / Basic tokens
            sanitized = RedactionRules[3].Replace(sanitized, "$1 [REDACTED]");

            return sanitized;
        }

        /// <summary>
        /// Redacts an exception's message and stack trace if sensitive data might be reflected.
        /// </summary>
        public static string RedactException(Exception ex)
        {
            if (ex == null) return string.Empty;
            return Redact(ex.ToString());
        }
    }
}
