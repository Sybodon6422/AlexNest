using Microsoft.Win32;
using System.Collections.Generic;
using System.Windows;
using AlexNest.Core.Algorithms;
using AlexNest.Core.Model;
using AlexNest.IO.Dxf;

namespace AlexNest.wpf;

public partial class MainWindow : Window
{
    private List<NestPart> _parts = new();
    private NestPlate? _plate;
    private NestingResult? _result;

    public MainWindow()
    {
        InitializeComponent();
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
                // You can tweak options: layer filters, etc.
                _parts = DXFPartImporter.ImportParts(dlg.FileName, new DxfPartImportOptions
                {
                    // PartLayers = new List<string> { "PARTS" }, // optional
                    EachClosedShapeIsPart = true
                });

                MessageBox.Show($"Loaded {_parts.Count} parts from {dlg.FileName}", "Parts Loaded");
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
                    PlateOuterLayer = "PLATE", // adjust to match your DXF
                    VoidLayer = "VOID"
                });

                Viewer.Plate = _plate;
                Viewer.Result = null;
                Viewer.InvalidateVisual();

                MessageBox.Show($"Loaded plate from {dlg.FileName} ({_plate.Width:0.##} x {_plate.Height:0.##})", "Plate Loaded");
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
            MessageBox.Show("Load a plate DXF first.", "No Plate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_parts.Count == 0)
        {
            MessageBox.Show("Load part DXF(s) first.", "No Parts", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nester = new GridNester();
        var settings = new GridNesterSettings
        {
            GridStep = 5,
            Clearance = 2,
            Kerf = 1.5
        };

        _result = nester.Nest(_parts, _plate, settings);
        Viewer.Plate = _plate;
        Viewer.Result = _result;
        Viewer.InvalidateVisual();

        MessageBox.Show($"Placed: {_result.Placements.Count}, Unplaced: {_result.UnplacedParts.Count}", "Nesting Done");
    }
}
