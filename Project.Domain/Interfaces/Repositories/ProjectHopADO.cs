using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Project.Domain.Interfaces.ADO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Project.Domain.Interfaces.Repositories
{
    public class ProjectHopADO :IADO
    {
        private string connectionString;
        private readonly IConfiguration _configuration;
        public ProjectHopADO(IConfiguration configuration)
        {
            configuration = configuration;
            connectionString = _configuration.GetConnectionString("dbcontext") + "";
        }

        public DataTable dataTable(string sql)
        {
            DataTable dt = new DataTable();
            using (var _connect = new SqlConnection(connectionString))
            using (SqlDataAdapter da = new SqlDataAdapter(sql, _connect))
            {
                da.Fill(dt);
            }
            return dt;
        }

        public async Task<DataTable> dataTableAsync(string sql)
        {
            DataTable dt = new DataTable();
            using var _connect = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(sql, _connect);
            using SqlDataAdapter da = new SqlDataAdapter(cmd);
            await _connect.OpenAsync();
            da.Fill(dt);
            return dt;
        }

        public async Task<bool> CmdAsync(string sql)
        {
            using (var _connect = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, _connect))
                return await command.ExecuteNonQueryAsync() == 1;
        }
        public bool Cmd(string sql)
        {
            using (var _connect = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, _connect))
                return command.ExecuteNonQuery() == 1;
        }

        public List<T> dataTable<T>(string command)
        {
            DataTable dt = dataTable(command);
            return ConvertDataTable<T>(dt);
        }
        public async Task<List<T>> dataTableAsync<T>(string command)
        {
            DataTable dt = await dataTableAsync(command);
            return ConvertDataTable<T>(dt);
        }

        private List<T> ConvertDataTable<T>(DataTable dt)
        {
            List<T> data = new List<T>();
            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);
                data.Add(item);
            }
            return data;
        }

        private T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)

                        if (dr[column.ColumnName] != null)
                        {
                            pro.SetValue(obj, dr[pro.Name] == DBNull.Value ? null : dr[pro.Name]);
                        }

                        else
                            continue;
                }
            }
            return obj;
        }

        public DataTable dataTableWithParameters(string sql, SqlParameter[] parameter)
        {
            DataTable dt = new DataTable();
            using SqlConnection _connect = new SqlConnection(connectionString);
            using SqlCommand cmd = new SqlCommand(sql, _connect);
            cmd.CommandTimeout = 3000;
            if (parameter != null && parameter.Length > 0)
            {
                for (int i = 0; i < parameter.Length; i++)
                {
                    cmd.Parameters.Add(new SqlParameter(parameter[i].ParameterName, parameter[i].Value));
                }
            }
            using SqlDataAdapter da = new SqlDataAdapter(cmd);
            _connect.Open();
            da.Fill(dt);
            return dt;
        }

        public async Task<DataTable> dataTableWithParametersAsync(string sql, SqlParameter[] parameter)
        {
            DataTable dt = new DataTable();
            using var _connect = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(sql, _connect);
            cmd.CommandTimeout = 3000;
            if (parameter != null && parameter.Length > 0)
            {
                for (int i = 0; i < parameter.Length; i++)
                {
                    cmd.Parameters.Add(new SqlParameter(parameter[i].ParameterName, parameter[i].Value));
                }
            }
            using SqlDataAdapter da = new SqlDataAdapter(cmd);
            await _connect.OpenAsync();

            da.Fill(dt);
            return dt;
        }

        public List<T> dataTableWithParameters<T>(string sql, SqlParameter[] parameter)
        {
            DataTable dt = dataTableWithParameters(sql, parameter);
            return ConvertDataTable<T>(dt);
        }

        public async Task<List<T>> dataTableWithParametersAsync<T>(string sql, SqlParameter[] parameter)
        {
            DataTable dt = await dataTableWithParametersAsync(sql, parameter);
            return ConvertDataTable<T>(dt);
        }


        public List<TableSys> getForeignKeyTable(string tableName)
        {
            string qr_check = string.Format(@"SELECT 
                       OBJECT_NAME(f.parent_object_id) TableName,
                       COL_NAME(fc.parent_object_id,fc.parent_column_id) ColName
                    FROM 
                       sys.foreign_keys AS f
                    INNER JOIN 
                       sys.foreign_key_columns AS fc 
                          ON f.OBJECT_ID = fc.constraint_object_id
                    INNER JOIN 
                       sys.tables t 
                          ON t.OBJECT_ID = fc.referenced_object_id
                    WHERE 
                       OBJECT_NAME (f.referenced_object_id) = '{0}'", tableName);

            var dt = dataTable(qr_check).AsEnumerable().Select(c => new TableSys
            {
                TableName = c.Field<String>("TableName"),
                ColName = c.Field<string>("ColName")
            }).ToList();
            return dt;
        }


        public async Task<List<TableSys>> getForeignKeyTableAsync(string tableName)
        {
            string qr_check = string.Format(@"SELECT 
                       OBJECT_NAME(f.parent_object_id) TableName,
                       COL_NAME(fc.parent_object_id,fc.parent_column_id) ColName
                    FROM 
                       sys.foreign_keys AS f
                    INNER JOIN 
                       sys.foreign_key_columns AS fc 
                          ON f.OBJECT_ID = fc.constraint_object_id
                    INNER JOIN 
                       sys.tables t 
                          ON t.OBJECT_ID = fc.referenced_object_id
                    WHERE 
                       OBJECT_NAME (f.referenced_object_id) = '{0}'", tableName);

            var dt = (await dataTableAsync(qr_check)).AsEnumerable().Select(c => new TableSys
            {
                TableName = c.Field<String>("TableName"),
                ColName = c.Field<string>("ColName")
            }).ToList();
            return dt;
        }

        public bool CheckForeignKeyTable(string tableName, object valuecheck, string type = "string")
        {
            bool check = true;

            var foreign = getForeignKeyTable(tableName);
            if (foreign.Count > 0)
            {
                bool run_check = true;
                int i = 0;
                while (run_check && i < foreign.Count)
                {
                    TableSys tableNow = foreign[i];
                    string qr_check = "";
                    if (type == "string")
                    {
                        qr_check = string.Format("SELECT * FROM {0} WHERE {1} =  N'{2}'", tableNow.TableName, tableNow.ColName, valuecheck);
                    }
                    else if (type == "number")
                    {
                        qr_check = string.Format("SELECT * FROM {0} WHERE {1} =  {2}", tableNow.TableName, tableNow.ColName, valuecheck);
                    }

                    var data_check = dataTable(qr_check);
                    if (data_check.Rows.Count > 0)
                    {
                        run_check = false;
                        check = false;
                    }

                    i++;
                }
            }
            else
            {
                check = true;
            }

            return check;
        }

        public async Task<bool> CheckForeignKeyTableAsync(string tableName, object valuecheck, string type = "string")
        {
            bool check = true;

            var foreign = await getForeignKeyTableAsync(tableName);
            if (foreign.Count > 0)
            {
                bool run_check = true;
                int i = 0;
                while (run_check && i < foreign.Count)
                {
                    TableSys tableNow = foreign[i];
                    string qr_check = "";
                    if (type == "string")
                    {
                        qr_check = string.Format("SELECT * FROM {0} WHERE {1} =  N'{2}'", tableNow.TableName, tableNow.ColName, valuecheck);
                    }
                    else if (type == "number")
                    {
                        qr_check = string.Format("SELECT * FROM {0} WHERE {1} =  {2}", tableNow.TableName, tableNow.ColName, valuecheck);
                    }

                    var data_check = dataTable(qr_check);
                    if (data_check.Rows.Count > 0)
                    {
                        run_check = false;
                        check = false;
                    }

                    i++;
                }
            }
            else
            {
                check = true;
            }

            return check;
        }
    }
    public class TableSys
    {
        public string TableName { get; set; }
        public string ColName { get; set; }
    }
}
