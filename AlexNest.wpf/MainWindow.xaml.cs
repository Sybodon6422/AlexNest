using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using AlexNest.Core.Algorithms;
using AlexNest.Core.Model;
using AlexNest.IO.DXF;

namespace AlexNest.wpf;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private List<NestPart> _partsRaw = new();
    private NestPlate? _plate;
    private NestingResult? _result;

    public ObservableCollection<PartViewModel> Parts { get; } = new();

    private double _gridStep = 5.0;
    public double GridStep
    {
        get => _gridStep;
        set { if (SetField(ref _gridStep, value)) { } }
    }

    private double _clearance = 2.0;
    public double Clearance
    {
        get => _clearance;
        set { if (SetField(ref _clearance, value)) { } }
    }

    private double _kerf = 1.5;
    public double Kerf
    {
        get => _kerf;
        set { if (SetField(ref _kerf, value)) { } }
    }

    private string _plateInfo = "No plate loaded.";
    public string PlateInfo
    {
        get => _plateInfo;
        set => SetField(ref _plateInfo, value);
    }

    private string _selectedAlgorithm = "Grid";
    public string SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set => SetField(ref _selectedAlgorithm, value);
    }

    private bool _allowMirror = false;
    public bool AllowMirror
    {
        get => _allowMirror;
        set => SetField(ref _allowMirror, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void BtnLoadParts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DXF Files|*.dxf|All Files|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _partsRaw = DxfPartImporter.ImportParts(dlg.FileName, new DxfPartImportOptions
                {
                    // tweak as you like:
                    EachClosedShapeIsPart = false,
                    SinglePartName = "Part"
                });

                Parts.Clear();
                foreach (var p in _partsRaw)
                {
                    if (p.Quantity <= 0)
                        p.Quantity = 1;

                    Parts.Add(new PartViewModel(p));
                }

                MessageBox.Show($"Loaded {_partsRaw.Count} parts from {dlg.FileName}", "Parts Loaded");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading parts: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnLoadPlate_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "DXF Files|*.dxf|All Files|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _plate = DxfPlateImporter.ImportPlate(dlg.FileName, new DxfPlateImportOptions
                {
                    PlateOuterLayer = null // accept any layer for plate outline
                });

                Viewer.Plate = _plate;
                Viewer.Result = null;
                Viewer.InvalidateVisual();

                PlateInfo = $"Plate: {_plate.Width:0.##} x {_plate.Height:0.##}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading plate: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnRunNest_Click(object sender, RoutedEventArgs e)
    {
        if (_plate == null)
        {
            MessageBox.Show("Load a plate DXF first.", "No Plate",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_partsRaw.Count == 0)
        {
            MessageBox.Show("Load part DXF(s) first.", "No Parts",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // sync quantities UI -> model
        foreach (var vm in Parts)
            vm.ApplyToPart();

        // pick algorithm
        INester nester = SelectedAlgorithm == "Strip"
            ? new StripNester()
            : new GridNester();

        var settings = new GridNesterSettings
        {
            GridStep = GridStep,
            Clearance = Clearance,
            Kerf = Kerf,
            AllowMirror = AllowMirror
        };

        try
        {
            _result = nester.Nest(_partsRaw, _plate, settings);

            Viewer.Plate = _plate;
            Viewer.Result = _result;
            Viewer.InvalidateVisual();

            MessageBox.Show(
                $"Placed: {_result.Placements.Count}, Unplaced: {_result.UnplacedParts.Count}",
                "Nesting Done");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during nesting: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    #endregion
}

/// <summary>
/// View model wrapper for a NestPart, so we can edit Quantity in the UI
/// without breaking the core model.
/// </summary>
public class PartViewModel : INotifyPropertyChanged
{
    public NestPart Part { get; }

    public string Name => Part.Name;
    public double Width => Part.Bounds.Width;
    public double Height => Part.Bounds.Height;

    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set
        {
            var clamped = Math.Max(0, value);
            if (SetField(ref _quantity, clamped))
            {
                Part.Quantity = Math.Max(1, clamped); // keep model sane
            }
        }
    }

    public PartViewModel(NestPart part)
    {
        Part = part;
        _quantity = part.Quantity <= 0 ? 1 : part.Quantity;
    }

    public void ApplyToPart()
    {
        Part.Quantity = Math.Max(1, Quantity);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
