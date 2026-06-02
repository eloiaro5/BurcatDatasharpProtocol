using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Provides GUID helpers used by the Burcat protocol.
    /// </summary>
    public static class GuidExtensions
    {
        /// <summary>
        /// Generates a time-ordered GUID with random trailing bytes.
        /// </summary>
        /// <returns>A sequential GUID value.</returns>
        public static Guid GenerateSequential()
        {
            byte[] bytes = new byte[16];
            long unixMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            bytes[0] = (byte)(unixMillis >> 40);
            bytes[1] = (byte)(unixMillis >> 32);
            bytes[2] = (byte)(unixMillis >> 24);
            bytes[3] = (byte)(unixMillis >> 16);
            bytes[4] = (byte)(unixMillis >> 8);
            bytes[5] = (byte)(unixMillis);

            byte[] randomBytes = new byte[10];
            RandomNumberGenerator.Create().GetBytes(randomBytes);
            Array.Copy(randomBytes, 0, bytes, 6, 10);

            bytes[6] &= 0x0F;
            bytes[6] |= 0x70;
            bytes[8] &= 0x3F;
            bytes[8] |= 0x80;

            return new Guid(bytes);
        }

        /// <summary>
        /// Generates a random GUID from cryptographically secure random bytes.
        /// </summary>
        /// <returns>A random GUID value.</returns>
        public static Guid GenerateRandom() => new(RandomNumberGenerator.GetBytes(16));

        /// <summary>
        /// Adds a byte value to a GUID interpreted as a little-endian byte sequence.
        /// </summary>
        /// <param name="guid">The base GUID.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>The resulting GUID.</returns>
        public static Guid Add(Guid guid, byte value)
        {
            int carry = value;
            byte[] bytes = guid.ToByteArray();
            for (int i = 0; i < bytes.Length; i++)
            {
                int sum = bytes[i] + carry;
                bytes[i] = (byte)(sum & 0xFF);
                carry = sum >> 8;

                if (carry == 0) break;
            }

            return new Guid(bytes);
        }
    }
}
