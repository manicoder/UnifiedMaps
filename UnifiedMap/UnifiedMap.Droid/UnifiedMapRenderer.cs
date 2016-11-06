using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.OS;
using Android.Util;
using fivenine.UnifiedMaps;
using fivenine.UnifiedMaps.Droid;
using Java.Lang;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(UnifiedMap), typeof(UnifiedMapRenderer))]

namespace fivenine.UnifiedMaps.Droid
{
    public class UnifiedMapRenderer : ViewRenderer<UnifiedMap, MapView>, GoogleMap.IOnCameraChangeListener,
        IOnMapReadyCallback, GoogleMap.IOnInfoWindowClickListener, GoogleMap.IOnMarkerClickListener, IUnifiedMapRenderer
    {
        private static Bundle _bundle;
        private readonly RendererBehavior _behavior;

        private readonly Dictionary<IMapAnnotation, Marker> _markers;
        private readonly Dictionary<IPolylineOverlay, Polyline> _polylines;
        private readonly Dictionary<ICircleOverlay, Circle> _circles;

        private GoogleMap _googleMap;

        public UnifiedMapRenderer()
        {
            AutoPackage = false;
            _markers = new Dictionary<IMapAnnotation, Marker>();
            _polylines = new Dictionary<IPolylineOverlay, Polyline>();
            _circles = new Dictionary<ICircleOverlay, Circle>();

            _behavior = new RendererBehavior(this);
        }

        internal static Bundle Bundle
        {
            set { _bundle = value; }
        }

        protected virtual Thickness MapPadding { get; } = new Thickness(48);

        public void OnInfoWindowClick(Marker marker)
        {
            var mapPin = _markers.Values
                .FirstOrDefault(val => val.EqualsSafe(marker));

            var command = Element.PinCalloutTappedCommand;
            if (command != null && command.CanExecute(mapPin))
            {
                command.Execute(mapPin);
            }
        }

        public bool OnMarkerClick(Marker marker)
        {
            var mapPin = _markers
                .Where(kv => kv.Value.Id.Equals(marker.Id))
                .Select(kv => kv.Key as IMapPin)
                .FirstOrDefault();

            Marker selectedMarker;
            var selectedPin = Map.SelectedItem;

            if (_markers.TryGetValue(selectedPin, out selectedMarker))
            {
                UpdateMarkerImage(selectedPin as IMapPin, selectedMarker, false)
                    .HandleExceptions();
            }

            UpdateMarkerImage(mapPin, marker, true)
                .HandleExceptions();
            
            Map.SelectedItem = mapPin;

            if (!string.IsNullOrWhiteSpace(mapPin.Title))
            {
                marker.ShowInfoWindow();
            }

            return true;
        }

        public void OnCameraChange(CameraPosition position)
        {
            if (_googleMap == null)
            {
                return;
            }

            var mapRegion = _googleMap.Projection.VisibleRegion.LatLngBounds;
            var region = new MapRegion(mapRegion.Northeast.Latitude, mapRegion.Southwest.Longitude,
                                       mapRegion.Southwest.Latitude, mapRegion.Northeast.Longitude);

            Map.VisibleRegion = region;
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            _googleMap = googleMap;

            // Register listeners
            _googleMap.SetOnInfoWindowClickListener(this);
            _googleMap.SetOnMarkerClickListener(this);

            ApplyPadding();
            _behavior.Initialize();
        }

        public UnifiedMap Map => Element;

        public void MoveToRegion(MapRegion region, bool animated)
        {
            if (_googleMap == null)
            {
                return;
            }

            var cameraUpdate = CameraUpdateFactory.NewLatLngBounds(region.ToBounds(), 0);

            try
            {
                if (animated)
                {
                    _googleMap.AnimateCamera(cameraUpdate);
                }
                else {
                    _googleMap.MoveCamera(cameraUpdate);
                }
            }
            catch (IllegalStateException)
            {
            }
        }

        public void AddPin(IMapPin pin)
        {
            if (_googleMap == null)
                return;

            AddPinAsync(pin)
                .HandleExceptions();
        }

        public void RemovePin(IMapPin pin)
        {
            if (_googleMap == null || pin == null)
                return;

            Marker marker = null;
            if (_markers.TryGetValue(pin, out marker))
            {
                marker.Remove();
                _markers.Remove(pin);
            }
        }

        public void AddOverlay(IMapOverlay item)
        {
            if (_googleMap == null)
            {
                return;
            }

            if (item is ICircleOverlay)
            {
                var circle = (ICircleOverlay)item;

                var options = new CircleOptions();
                options.InvokeCenter(circle.Location.ToLatLng());
                options.InvokeRadius(circle.Radius);
                options.InvokeStrokeWidth(circle.LineWidth);
                options.InvokeStrokeColor(circle.StrokeColor.ToAndroid().ToArgb());
                options.InvokeFillColor(circle.FillColor.ToAndroid().ToArgb());

                var mapCircle = _googleMap.AddCircle(options);
                _circles.Add(circle, mapCircle);
                return;
            }

            if (item is IPolylineOverlay)
            {
                var polyline = (IPolylineOverlay)item;
                
                var options = new PolylineOptions();
                options.Add(polyline.Select(p => p.ToLatLng()).ToArray());
                options.InvokeColor(polyline.StrokeColor.ToAndroid());
                options.InvokeWidth(polyline.LineWidth);

                var mapPolyline = _googleMap.AddPolyline(options);
                _polylines.Add(polyline, mapPolyline);
                return;
            }
        }

        public void RemoveOverlay(IMapOverlay item)
        {
            if (item is ICircleOverlay)
            {
                var circle = (ICircleOverlay)item;
                Circle mapCircle;
                if (_circles.TryGetValue(circle, out mapCircle))
                {
                    _circles.Remove(circle);
                    mapCircle.Remove();
                }

                return;
            }

            if (item is IPolylineOverlay)
            {
                var polyline = (IPolylineOverlay)item;
                Polyline mapPolyline;
                if (_polylines.TryGetValue(polyline, out mapPolyline))
                {
                    _polylines.Remove(polyline);
                    mapPolyline.Remove();
                }

                return;
            }
        }

        public void FitAllAnnotations(bool animated)
        {
            var region = _behavior.GetRegionForAllAnnotations();
            MoveToRegion(region, animated);
        }

        public void ApplyHasZoomEnabled()
        {
            if (_googleMap != null)
            {
                _googleMap.UiSettings.ZoomGesturesEnabled = Element.HasZoomEnabled;
                _googleMap.UiSettings.ZoomControlsEnabled = Element.HasZoomEnabled;
            }
        }

        public void ApplyHasScrollEnabled()
        {
            if (_googleMap != null)
            {
                _googleMap.UiSettings.ScrollGesturesEnabled = Element.HasScrollEnabled;
            }
        }

        public void ApplyIsShowingUser()
        {
            if (_googleMap != null)
            {
                _googleMap.UiSettings.MyLocationButtonEnabled = Element.IsShowingUser;
            }
        }

        public void ApplyMapType()
        {
            if (_googleMap != null)
            {
                switch (Element.MapType)
                {
                    case MapType.Street:
                        _googleMap.MapType = GoogleMap.MapTypeNormal;
                        break;
                    case MapType.Satellite:
                        _googleMap.MapType = GoogleMap.MapTypeSatellite;
                        break;
                    case MapType.Hybrid:
                        _googleMap.MapType = GoogleMap.MapTypeHybrid;
                        break;
                    default:
                        {
                            _googleMap.MapType = GoogleMap.MapTypeNormal;
                            Log.Error("error",
                                $"The map type {Element.MapType} is not supported on Android, falling back to Street");
                            break;
                        }
                }
            }
        }

        public void SetSelectedAnnotation()
        {
            Marker selectedMarker;
            if (_markers.TryGetValue(Map.SelectedItem, out selectedMarker))
            {
                OnMarkerClick(selectedMarker);
            }
        }

        protected override void OnElementChanged(ElementChangedEventArgs<UnifiedMap> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement != null)
            {
                var element = e.OldElement;

                _googleMap?.Dispose();
                RemoveEvents(element);
            }

            if (e.NewElement != null)
            {
                RegisterEvents(e.NewElement);

                if (Control == null)
                {
                    var mapView = new MapView(Context);

                    mapView.OnCreate(_bundle);
                    mapView.OnResume();

                    SetNativeControl(mapView);

                    mapView.GetMapAsync(this);
                }
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);
            _behavior.ElementProperyChanged(e.PropertyName);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveEvents(Element);

                if (_googleMap != null)
                {
                    _googleMap.Dispose();
                    _googleMap = null;
                }
            }

            base.Dispose(disposing);
        }

        private async Task AddPinAsync(IMapPin pin)
        {
            if (_markers.ContainsKey(pin))
            {
                return;
            }

            var mapPin = new MarkerOptions();

            if (!string.IsNullOrWhiteSpace(pin.Title))
            {
                mapPin.SetTitle(pin.Title);
            }

            if (!string.IsNullOrWhiteSpace(pin.Snippet))
            {
                mapPin.SetSnippet(pin.Snippet);
            }

            mapPin.SetPosition(new LatLng(pin.Location.Latitude, pin.Location.Longitude));

            if (pin.Image != null)
            {
                mapPin.Anchor((float)pin.Anchor.X, (float)pin.Anchor.Y);
            }

            var markerView = _googleMap.AddMarker(mapPin);
            _markers.Add(pin, markerView);

            await UpdateMarkerImage(pin, markerView, pin.EqualsSafe(Map.SelectedItem));
        }

        private void RegisterEvents(UnifiedMap map)
        {
            _behavior.RegisterEvents(map);
        }

        private void RemoveEvents(UnifiedMap map)
        {
            _behavior.RemoveEvents(map);
        }

        private void ApplyPadding()
        {
            var padding = MapPadding;
            _googleMap.SetPadding((int)padding.Left, (int)padding.Top, (int)padding.Right, (int)padding.Bottom);
        }

        private async Task UpdateMarkerImage(IMapPin pin, Marker mapPin, bool selected)
        {
            if (pin == null)
            {
                return;
            }

            BitmapDescriptor icon;

            if (pin.Image != null)
            {
                var image = selected && pin.SelectedImage != null ? pin.SelectedImage : pin.Image;
                icon = BitmapDescriptorFactory.FromBitmap(await image.ToBitmap(Context));
            }
            else
            {
                var color = selected ? pin.SelectedColor : pin.Color;
                icon = color.ToStandardMarkerIcon();
            }

            if (icon == null)
            {
                icon = BitmapDescriptorFactory.DefaultMarker();
            }

            mapPin.SetIcon(icon);
        }
    }
}