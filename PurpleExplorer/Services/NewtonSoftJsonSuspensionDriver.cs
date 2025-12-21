using Newtonsoft.Json;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;

namespace PurpleExplorer.Services;

public class NewtonsoftJsonSuspensionDriver : ISuspensionDriver
{
    private readonly string _file;
    private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.All,
        Formatting = Formatting.Indented
    };

    public NewtonsoftJsonSuspensionDriver(string file) => _file = file;

    public IObservable<Unit> InvalidateState()
    {
        if (File.Exists(_file))
        {
            var initial = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Detected invalid state. Will move {_file} to {_file}.broken");
            Console.ForegroundColor = initial;

            try
            {
                if (File.Exists(_file + ".broken"))
                {
                    File.Delete(_file + ".broken");
                }
                File.Move(_file, _file + ".broken");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving invalid state file: {ex.Message}");
            }
        }

        return Observable.Return(Unit.Default);
    }

    public IObservable<object> LoadState()
    {
        try
        {
            if (File.Exists(_file))
            {
                var lines = File.ReadAllText(_file);
                var state = JsonConvert.DeserializeObject<object>(lines, _settings);
                if (state != null)
                {
                    return Observable.Return(state);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading state from {_file}: {ex.Message}");
        }

        var backupFile = _file + ".backup";
        if (File.Exists(backupFile))
        {
            try
            {
                Console.WriteLine($"Attempting to load state from backup: {backupFile}");
                var lines = File.ReadAllText(backupFile);
                var state = JsonConvert.DeserializeObject<object>(lines, _settings);
                if (state != null)
                {
                    return Observable.Return(state);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading state from backup {backupFile}: {ex.Message}");
            }
        }

        return Observable.Throw<object>(new FileNotFoundException(_file));
    }

    public IObservable<Unit> SaveState(object state)
    {
        var lines = JsonConvert.SerializeObject(state, _settings);
        Console.WriteLine($"Saving state: will write {lines.Length} lines to {_file}");

        var backupFile = _file + ".backup";
        var tempFile = _file + ".tmp";

        try
        {
            File.WriteAllText(tempFile, lines);
            if (File.Exists(_file))
            {
                File.Copy(_file, backupFile, true);
            }
            File.Move(tempFile, _file, true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving state: {ex.Message}");
        }

        return Observable.Return(Unit.Default);
    }
}