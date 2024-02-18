using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using MySqlConnector;
using Serilog;
using Serilog.Events;

namespace MyGIS;

class DbManager
{
    private static MySqlConnection _connection;

    static DbManager()
    {
        Connect();

        Log.Information("Create mysql connection");
    }

    private static void Connect()
    {
        string user1 = "z9Cx",
            user2 = "z9Cx6355";
        string connectionString = $"server=127.0.0.1; port=3306; username=root; password={user2}; database=gis";

        Connection = new MySqlConnection(connectionString);
    }

    public static MySqlConnection Connection { get => _connection; private set => _connection = value; }

    public static async Task<MySqlDataReader> ExecuteCommand(string _command, List<MySqlParameter>? parameters=null, MySqlTransaction _transaction=null)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = _command;

        if (parameters != null)
        {
            command.Parameters.Clear();
            for (int i = 0; i < parameters.Count; i++)
            {
                MySqlParameter parameter = parameters[i];
                command.Parameters.Add(parameter);
            }
            
        }

        var result = await command.ExecuteReaderAsync();
        Log.Information("Execute command {0}", _command);

        await command.DisposeAsync();

        return result;
    }

    public static async Task CloseConnection()
    {
        
        await _connection.CloseAsync();
        Log.Information("Close database connection");

    }

    public static async Task OpenConnection()
    {
        try
        {
            await _connection.OpenAsync();

            Log.Information("Open database connection");
        }
        catch 
        {
            
        }
    }
}
