using Project.Domain.Interfaces.Crypt;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public class ProjectHopEncrypt :ICrypt
    {
        private string ExtensionCryptor;
        public ProjectHopEncrypt()
        {
            ExtensionCryptor = ".dtsoft";
        }

        public string EncryptString(string input)
        {
            string data = input;
            byte[] utfdata = UTF8Encoding.UTF8.GetBytes(data);
            byte[] saltBytes = UTF8Encoding.UTF8.GetBytes("DTSoftKG295");

            // Our symmetric encryption algorithm
            AesManaged aes = new AesManaged();

            // We're using the PBKDF2 standard for password-based key generation
            Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes("092240", saltBytes);

            // Setting our parameters
            aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
            aes.KeySize = aes.LegalKeySizes[0].MaxSize;

            aes.Key = rfc.GetBytes(aes.KeySize / 8);
            aes.IV = rfc.GetBytes(aes.BlockSize / 8);

            // Encryption
            ICryptoTransform encryptTransf = aes.CreateEncryptor();

            // Output stream, can be also a FileStream
            MemoryStream encryptStream = new MemoryStream();
            CryptoStream encryptor = new CryptoStream(encryptStream, encryptTransf, CryptoStreamMode.Write);

            encryptor.Write(utfdata, 0, utfdata.Length);
            encryptor.Flush();
            encryptor.Close();

            // Showing our encrypted content
            byte[] encryptBytes = encryptStream.ToArray();
            //string encryptedString = UTF8Encoding.UTF8.GetString(encryptBytes, 0, encryptBytes.Length);
            string encryptedString = Convert.ToBase64String(encryptBytes);
            Debug.WriteLine(encryptedString);

            return encryptedString;
        }


        public string DecryptString(string base64Input)
        {

            //byte[] encryptBytes = UTF8Encoding.UTF8.GetBytes(input);
            byte[] encryptBytes = Convert.FromBase64String(base64Input);
            byte[] saltBytes = Encoding.UTF8.GetBytes("DTSoftKG295");

            // Our symmetric encryption algorithm
            AesManaged aes = new AesManaged();

            // We're using the PBKDF2 standard for password-based key generation
            Rfc2898DeriveBytes rfc = new Rfc2898DeriveBytes("092240", saltBytes);

            // Setting our parameters
            aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
            aes.KeySize = aes.LegalKeySizes[0].MaxSize;

            aes.Key = rfc.GetBytes(aes.KeySize / 8);
            aes.IV = rfc.GetBytes(aes.BlockSize / 8);

            // Now, decryption
            ICryptoTransform decryptTrans = aes.CreateDecryptor();

            // Output stream, can be also a FileStream
            MemoryStream decryptStream = new MemoryStream();
            CryptoStream decryptor = new CryptoStream(decryptStream, decryptTrans, CryptoStreamMode.Write);

            decryptor.Write(encryptBytes, 0, encryptBytes.Length);
            decryptor.Flush();
            decryptor.Close();

            // Showing our decrypted content
            byte[] decryptBytes = decryptStream.ToArray();
            string decryptedString = UTF8Encoding.UTF8.GetString(decryptBytes, 0, decryptBytes.Length);
            Debug.WriteLine(decryptedString);

            return decryptedString;
        }

        private Byte[] GenerateRandomSalt()
        {
            byte[] data = new byte[32];
            using (RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider())
            {
                for (int I = 0; I < 10; I++)
                {
                    RNG.GetBytes(data);

                }

            }
            return data;
        }



        public void EncryptFile(string inputFile, string password)
        {
            byte[] salt = GenerateRandomSalt();
            FileStream fsCrypt = new FileStream(inputFile + this.ExtensionCryptor, FileMode.Create);
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;
            AES.Padding = PaddingMode.PKCS7;

            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Mode = CipherMode.CFB;

            fsCrypt.Write(salt, 0, salt.Length);
            CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateEncryptor(), CryptoStreamMode.Write);
            FileStream fsIn = new FileStream(inputFile, FileMode.Open);
            byte[] buffer = new byte[1048576];
            int read;
            try
            {
                while ((read = fsIn.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cs.Write(buffer, 0, read);
                }
                fsIn.Close();
            }
            catch (Exception ex)
            {

            }
            finally
            {
                cs.Close();
                fsCrypt.Close();
            }

        }


        public void DecryptFile(string inputFile, string outputFile, string password)
        {
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            byte[] salt = new byte[32];
            FileStream fsCrypt = new FileStream(inputFile, FileMode.Open);
            fsCrypt.Read(salt, 0, salt.Length);
            RijndaelManaged AES = new RijndaelManaged();
            AES.KeySize = 256;
            AES.BlockSize = 128;

            var key = new Rfc2898DeriveBytes(passwordBytes, salt, 50000);
            AES.Key = key.GetBytes(AES.KeySize / 8);
            AES.IV = key.GetBytes(AES.BlockSize / 8);
            AES.Padding = PaddingMode.PKCS7;
            AES.Mode = CipherMode.CFB;

            CryptoStream cs = new CryptoStream(fsCrypt, AES.CreateDecryptor(), CryptoStreamMode.Read);
            FileStream fsOut = new FileStream(outputFile, FileMode.Create);
            int read;
            byte[] buffer = new byte[1048576];
            try
            {
                while ((read = cs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fsOut.Write(buffer, 0, read);
                }
            }
            catch (Exception ex)
            {

            }
            try { cs.Close(); }
            catch (Exception ex)
            {

            }
            finally
            {
                fsOut.Close();
                fsCrypt.Close();
            }
        }

    }
}
