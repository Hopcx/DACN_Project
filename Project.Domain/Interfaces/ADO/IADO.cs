using Microsoft.Data.SqlClient;
using Project.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.ADO
{
    public interface IADO
    {
        /// <summary>
        /// Chạy câu query thuần và trả về là 1 datatable kết quả dữ liệu được chạy
        /// </summary>
        /// <param name="sql">câu lệnh query sẽ được chạy trên sql server thường là câu lệnh có kết quả trả về 1 danh sách dữ liệu</param>
        /// <returns></returns>
        DataTable dataTable(string sql);

        /// <summary>
        /// Chạy câu query thuần và trả về là 1 datatable kết quả dữ liệu được chạy
        /// </summary>
        /// <param name="sql">câu lệnh query sẽ được chạy trên sql server thường là câu lệnh có kết quả trả về 1 danh sách dữ liệu</param>
        /// <returns></returns>
        Task<DataTable> dataTableAsync(string sql);
        /// <summary>
        /// Chạy các câu lệnh query thuần , sau khi chạy xong sẽ trả về kết quả chạy có thành công hay không
        /// </summary>
        /// <param name="sql">chuỗi query sẽ chạy trên sql server</param>
        Task<bool> CmdAsync(string sql);
        /// <summary>
        /// Chạy các câu lệnh query thuần , sau khi chạy xong sẽ trả về kết quả chạy có thành công hay không
        /// </summary>
        /// <param name="sql">chuỗi query sẽ chạy trên sql server</param>
        bool Cmd(string sql);
        /// <summary>
        /// Chạy câu query thuần và trả về là 1 IEnumerable dữ liệu nhận từ kết quả câu lệnh query được chạy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command"></param>
        /// <returns></returns>
        List<T> dataTable<T>(string command);

        /// <summary>
        /// Chạy câu query thuần và trả về là 1 IEnumerable dữ liệu nhận từ kết quả câu lệnh query được chạy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command"></param>
        /// <returns></returns>
        Task<List<T>> dataTableAsync<T>(string command);

        /// <summary>
        /// Trả về datatable dữ liệu lấy từ sqlserver thông qua câu lệnh query + parameter (tránh injectsql)
        /// </summary>
        /// <param name="sql">lệnh sql server, các giá trị trong keyword select,where được truyền vào từ biến dùng @__</param>
        /// <param name="parameter">mảng danh sách parameter</param>
        /// <returns></returns>
        DataTable dataTableWithParameters(string sql, SqlParameter[] parameter);

        /// <summary>
        /// Trả về datatable dữ liệu lấy từ sqlserver thông qua câu lệnh query + parameter (tránh injectsql)
        /// </summary>
        /// <param name="sql">lệnh sql server, các giá trị trong keyword select,where được truyền vào từ biến dùng @__</param>
        /// <param name="parameter">mảng danh sách parameter</param>
        /// <returns></returns>
        Task<DataTable> dataTableWithParametersAsync(string sql, SqlParameter[] parameter);

        /// <summary>
        /// Trả về list dữ liệu lấy từ sqlserver thông qua câu lệnh query + parameter (tránh injectsql)
        /// </summary>
        /// <param name="sql">lệnh sql server, các giá trị trong keyword select,where được truyền vào từ biến dùng @__</param>
        /// <param name="parameter">
        List<T> dataTableWithParameters<T>(string sql, SqlParameter[] parameter);


        /// <summary>
        /// Trả về list dữ liệu lấy từ sqlserver thông qua câu lệnh query + parameter (tránh injectsql)
        /// </summary>
        /// <param name="sql">lệnh sql server, các giá trị trong keyword select,where được truyền vào từ biến dùng @__</param>
        /// <param name="parameter">
        Task<List<T>> dataTableWithParametersAsync<T>(string sql, SqlParameter[] parameter);

        /// <summary>
        /// Lây danh sách các bảng khóa ngoại với bảng hiện tại
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        List<TableSys> getForeignKeyTable(string tableName);

        /// <summary>
        /// Lây danh sách các bảng khóa ngoại với bảng hiện tại
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        Task<List<TableSys>> getForeignKeyTableAsync(string tableName);

        /// <summary>
        /// Trả về xem dữ liệu hiện tại có chứa dòng khóa ngoại nào không, nếu không có thì có thể xóa dữ liệu
        /// </summary>
        /// <param name="tableName">Tên bảng muốn check</param>
        /// <param name="valuecheck">khóa chính của bảng</param>
        /// <param name="type">kiểu dữ liệu khóa chính: string|number. mặc định là string</param>
        /// <returns></returns>
        bool CheckForeignKeyTable(string tableName, object valuecheck, string type = "string");

        /// <summary>
        /// Trả về xem dữ liệu hiện tại có chứa dòng khóa ngoại nào không, nếu không có thì có thể xóa dữ liệu
        /// </summary>
        /// <param name="tableName">Tên bảng muốn check</param>
        /// <param name="valuecheck">khóa chính của bảng</param>
        /// <param name="type">kiểu dữ liệu khóa chính: string|number. mặc định là string</param>
        /// <returns></returns>
        Task<bool> CheckForeignKeyTableAsync(string tableName, object valuecheck, string type = "string");
    }
    
}
