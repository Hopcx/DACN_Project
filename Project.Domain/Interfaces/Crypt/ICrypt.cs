using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Crypt
{
    public interface ICrypt
    {
        /// <summary>
        /// Thực hiện mã hóa dữ liệu chuỗi dữ liệu bằng Rfc2898
        /// </summary>
        /// <param name="input">chuỗi thường sẽ thực hiện mã hóa</param>
        /// <returns></returns>
        /// 
        string EncryptString(string input);
        /// <summary>
        ///  THỰC HIỆN GIẢI MÃ CHUỖI ĐÃ MÃ HÓA BẰNG rfc
        /// </summary>
        /// <param name="base64Input"></param>
        /// <returns></returns>
        string DecryptString(string base64Input);

        /// <summary>
        /// Thực hiện mã hóa file sang đuối .dtsoft, muốn mở và sử dụng file cần có khóa để giải mã
        /// </summary>
        /// <param name="inputFile">đường dẫn tới file: ví dụ: c:/file/fileA.pdf</param>
        /// <param name="password">MẬT KHẨU DÙNG ĐÃ MÃ HÓA VÀ GIẢI MÃ KHI CẦN</param>
        void EncryptFile(string inputFile, string password);
        /// <summary>
        /// Thực hiện giải mã file đã được mã hóa
        /// </summary>
        /// <param name="inputFile">đường dẫn tới file đã mã hóa</param>
        /// <param name="outputFile">đường dẫn file sau khi đã giải mã</param>
        /// <param name="password">mật khẩu đã mã hóa</param>
        void DecryptFile(string inputFile, string outputFile, string password);
    }
}
