﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using PolygonEditor.Desktop.Helpers;
using PolygonEditor.Desktop.Models;
using PolygonEditor.Desktop.Models.Constraints;
using PolygonEditor.Desktop.Models.Filler;
using PolygonEditor.Desktop.Models.InputHandlers;
using PolygonEditor.Desktop.Models.Intersections;

namespace PolygonEditor.Desktop.ViewModels
{
    public class PolygonViewModel : ObservableObject
    {
        private Vector2 prevMousePosition;


        private PolygonFillingSettings lastSettings = null;
        private  Polygon firstPolygon = new Polygon();
        private  Polygon secondPolygon = new Polygon(false);

        private PolygonFiller firstPolygonFiller;

        private bool autoConstraints;
        private InputHandler firstInputHandler;
        private InputHandler secondInputHandler;
        public BitmapImage BitmapCanvas { get; set; }
        public bool AutoConstraints
        {
            get
            {
                return autoConstraints;
            }
            set
            {
                autoConstraints = value;
                firstPolygon.AutoConstraints = value;
                secondPolygon.AutoConstraints = value;
            }
        }

        public PolygonFillingViewModel Filling { get; } = new PolygonFillingViewModel();

        public PolygonViewModel()
        {
            firstInputHandler = new EditorInputHandler(firstPolygon);
            secondInputHandler = new CreationInputHandler(secondPolygon);

            firstPolygon.AddVertex(100, 100);
            firstPolygon.AddVertex(300, 50);
            firstPolygon.AddVertex(500, 280);
            firstPolygon.AddVertex(800, 300);
            firstPolygon.AddVertex(400, 500);
            firstPolygon.AddVertex(50, 300);

            secondPolygon.AddVertex(130, 80);
            secondPolygon.AddVertex(200, 10);
            secondPolygon.AddVertex(800, 600);
            secondPolygon.AddVertex(100, 200);

            firstPolygon.SetClosed();
            secondPolygon.SetClosed();

            firstPolygonFiller = new PolygonFiller(firstPolygon);
            firstPolygonFiller.SetSettings(Filling.GetFillingSettings());
        }

        public ICommand Loaded => new RelayCommand(() =>
        {
            RedrawAllPolygons();
        });

        public ICommand DeleteKeyDown => new RelayCommand(() =>
        {
            if (secondPolygon.GetVertexes().Count() > 0)
            {
                secondPolygon = new Polygon(false);
                secondPolygon.AutoConstraints = autoConstraints;
                secondInputHandler = new CreationInputHandler(secondPolygon);
            }

            RedrawAllPolygons();

        });

        public ICommand MouseLeftDown => new RelayCommand<MouseButtonEventArgs>(x =>
        {
            int mouseX = (int) x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).X;
            int mouseY = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).Y;

            if (mouseX > GetMaxX())
                return;

            var result = firstInputHandler.MouseLeftDown(mouseX, mouseY);
            if(firstPolygon.IsClosed && firstInputHandler is CreationInputHandler)
                firstInputHandler = new EditorInputHandler(firstPolygon);

            if (!result && firstPolygon.IsClosed)
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                {
                    firstInputHandler.ResetLeftMousePressed();
                    secondInputHandler.MouseLeftDown(mouseX, mouseY);
                }
            }

        });

        public ICommand MouseLeftUp => new RelayCommand<MouseButtonEventArgs>(x =>
        {
            int mouseX = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).X;
            int mouseY = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).Y;


            firstInputHandler.MouseLeftUp(mouseX, mouseY);
            secondInputHandler.MouseLeftUp(mouseX, mouseY);

            RedrawAllPolygons();

        });

        public ICommand MouseRightDown => new RelayCommand<MouseButtonEventArgs>(x =>
        {
            int mouseX = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).X;
            int mouseY = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).Y;

            var result = firstInputHandler.MouseRightDown(mouseX, mouseY);
            if (!result)
            {
                secondInputHandler.MouseRightDown(mouseX, mouseY);
            }

            RedrawAllPolygons();
        });

        public ICommand MouseMove => new RelayCommand<MouseEventArgs>(x =>
        {
            int mouseX = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).X;
            int mouseY = (int)x.GetPosition((Application.Current.MainWindow as MainWindow).canvas).Y;

            var result1 = firstInputHandler.MouseMove(mouseX, mouseY);
            var result2 = secondInputHandler.MouseMove(mouseX, mouseY);

            if(result1 || result2)
                RedrawAllPolygons();

            if (Math.Abs(mouseX - prevMousePosition.X) > 10 || Math.Abs(mouseY - prevMousePosition.Y) > 10)
            {
                prevMousePosition.X = mouseX;
                prevMousePosition.Y = mouseY;

                if (lastSettings?.UseMouseFollowNormalVector == true)
                {
                    lastSettings.SetMouse(mouseX, mouseY);
                    RedrawAllPolygons();
                }
            }

        });

        public ICommand Resize => new RelayCommand(() =>
        {
            RedrawAllPolygons();
        });

        public ICommand ApplyFillingSettings => new RelayCommand(() =>
        {
            if(lastSettings != null)
                lastSettings.LightMoved -= LastSettings_LightMoved;

            lastSettings = Filling.GetFillingSettings();
            lastSettings.LightMoved += LastSettings_LightMoved;
            firstPolygonFiller.SetSettings(lastSettings);
            RedrawAllPolygons();
        });

        public ICommand ApplyClipping => new RelayCommand(() =>
        {
            if (secondPolygon.IsClosed && secondInputHandler is CreationInputHandler)
            {
                firstPolygon = PolygonIntersection.GetIntersectedPolygon(firstPolygon, secondPolygon);
                firstPolygonFiller = new PolygonFiller(firstPolygon);
                firstPolygonFiller.SetSettings(Filling.GetFillingSettings());
                firstInputHandler = new EditorInputHandler(firstPolygon);

                secondPolygon = new Polygon(false);
                secondInputHandler = new CreationInputHandler(secondPolygon);

                RedrawAllPolygons();
            }
        });

        private void LastSettings_LightMoved()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    RedrawAllPolygons();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }

        private void RedrawAllPolygons()
        {
            int width = (int)(Application.Current.MainWindow as MainWindow).ActualWidth;
            int height = (int)(Application.Current.MainWindow as MainWindow).ActualHeight;

            var bitmap = new Bitmap(width, height);
            bitmap.Fill(Color.White);

            RedrawPolygon(bitmap, firstPolygon);
            firstPolygonFiller.Fill(bitmap);
            RedrawPolygon(bitmap, secondPolygon);

            BitmapCanvas = bitmap.ConvertToBitmapImage();
            bitmap.Dispose();
            RaisePropertyChanged("BitmapCanvas");
        }

        private void RedrawPolygon(Bitmap bitmap, Polygon polygon)
        {
            foreach (var edge in polygon.GetEdges())
            {
                bitmap.DrawLine(edge.Item1, edge.Item2, Color.Black);
            }

            foreach (var vertex in polygon.GetVertexes())
            {
                var neighbours = polygon.GetNeighbours(vertex);
                bitmap.DrawCircle(vertex, 5, Color.Maroon);

                var angle = Vertex.AngleBetween(neighbours.Item1, vertex, neighbours.Item2);
                //bitmap.DrawText(vertex, angle.ToString(), Color.ForestGreen);
            }

            foreach (var vertexConstraint in polygon.GetConstraints())
            {
                var vertexes = vertexConstraint.GetVertexes().ToArray();

                if (vertexConstraint is VerticalEdgeConstraint)
                {
                    int x = (vertexes[0].X + vertexes[1].X) / 2 + 10;
                    int y = (vertexes[0].Y + vertexes[1].Y) / 2;
                    x += 10;
                    bitmap.DrawLine(new Vertex(x, y - 5), new Vertex(x, y + 5), Color.ForestGreen);
                    bitmap.DrawLine(new Vertex(x + 1, y - 5), new Vertex(x + 1, y + 5), Color.ForestGreen);
                }
                else if (vertexConstraint is HorizontalEdgeConstraint)
                {
                    int x = (vertexes[0].X + vertexes[1].X) / 2;
                    int y = (vertexes[0].Y + vertexes[1].Y) / 2 + 10;
                    y += 10;
                    bitmap.DrawLine(new Vertex(x - 5, y), new Vertex(x + 5, y), Color.ForestGreen);
                    bitmap.DrawLine(new Vertex(x - 5, y + 1), new Vertex(x + 5, y + 1), Color.ForestGreen);
                }
                else if (vertexConstraint is AngleConstraint)
                {
                    var v = vertexConstraint.GetVertexes().ToArray();
                    Vertex middle = v[1];

                    bitmap.DrawCircle(middle, 5, Color.ForestGreen);
                }
            }
        }

        private double GetMaxX()
        {
            return (Application.Current.MainWindow as MainWindow).grid.ColumnDefinitions[0].ActualWidth;
        }
    }
}
