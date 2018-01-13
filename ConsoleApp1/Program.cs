using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;

namespace ConsoleApp1
{
    interface IIntersectable
    {
        Tuple<bool, float> Intersect(Ray ray);
    }

    abstract class Element : IIntersectable
    {
        public Color Color;
        public Brush Brush;
        public float Albedo;

        protected Element(Color color)
        {
            Color = color;
            Brush = new SolidBrush(color);
            Albedo = 1f;
        }

        public abstract Tuple<bool, float> Intersect(Ray ray);
        public abstract Vector3 surface_normal(Vector3 hit_point);

        public Brush GetBrush(Scene scene, Ray ray, Intersection intersection)
        {
            var hitPoint = ray.Origin + (ray.Direction * intersection.Distance);
            var surfaceNormal = intersection.Element.surface_normal(hitPoint);
            var directionToLight = -Vector3.Normalize(scene.Light.Direction);
//            var light_power = Vector3.Dot(surface_normal,direction_to_light).max(0.0) * // todo check if this works
            var lightPower = Vector3.Dot(surfaceNormal, directionToLight) * scene.Light.Intensity;
            var lightReflected = intersection.Element.Albedo / Math.PI;


            var shadowRay = new Ray(hitPoint + (surfaceNormal * scene.shadowBias), -scene.Light.Direction);

            bool in_light = !Ray.Trace(scene,shadowRay,this).Item1;
            if (!in_light)
            {
                return Brushes.Black;
            }

            double r = intersection.Element.Color.R / 255f * scene.Light.Color.R / 255f * lightPower * lightReflected;
            double g = intersection.Element.Color.G / 255f * scene.Light.Color.G / 255f * lightPower * lightReflected;
            double b = intersection.Element.Color.B / 255f * scene.Light.Color.B / 255f * lightPower * lightReflected;
            var color = Color.FromArgb(255,
                (int) (r * 255) > 0 ? (int) (r * 255) : 0,
                (int) (g * 255) > 0 ? (int) (g * 255) : 0,
                (int) (b * 255) > 0 ? (int) (b * 255) : 0
            );

            return new SolidBrush(color); // todo : check if this creates issues
        }

//        // todo : turn into extension method
        public static float clamp(double color)
        {
            int max = 1;
            int min = 0;
            if (color < 0)
            {
                Console.WriteLine("smaller than 0");
                return 0;
            }
            if (color > 1)
            {
                Console.WriteLine("bigger than 1");
                return 255;
            }
            return (float) (color * 255);
        }
    }

    class Sphere : Element
    {
        public Vector3 Center;
        public float Radius;

        public Sphere(Vector3 center, float radius, Color color) : base(color)
        {
            Center = center;
            Radius = radius;
        }

        public override Tuple<bool, float> Intersect(Ray ray)
        {
            var l = this.Center - ray.Origin;
            var adj = Vector3.Dot(l, ray.Direction);
            var d2 = Vector3.Dot(l, l) - (adj * adj);

            var radius2 = this.Radius * this.Radius;
            if (d2 > radius2)
            {
                return new Tuple<bool, float>(false, 0);
            }
            var thc = Math.Sqrt(radius2 - d2);
            var t0 = adj - thc;
            var t1 = adj + thc;

            if (t0 < 0 && t1 < 0)
            {
                return new Tuple<bool, float>(false, 0);
            }
            var distance = t0 < t1 ? t0 : t1;
            return new Tuple<bool, float>(true, (float) distance);
        }

        public override Vector3 surface_normal(Vector3 hitPoint)
        {
            return Vector3.Normalize(hitPoint - this.Center);
        }
    }

    class Plane : Element
    {
        public Vector3 Origin;
        public Vector3 Normal;

        public Plane(Vector3 origin, Vector3 normal, Color color) : base(color)
        {
            Origin = origin;
            Normal = normal;
        }

        public override Tuple<bool, float> Intersect(Ray ray)
        {
            float denom = Vector3.Dot(this.Normal, ray.Direction);

            if (denom > 1e-6)
            {
                var v = this.Origin - ray.Origin; // todo : always 0 because the ray.origin is always 0

                float distance = Vector3.Dot(v, this.Normal) / denom;
                if (distance >= 0.0) // todo : distance is basically allways null for plance because v and normal 
                {
                    return new Tuple<bool, float>(true, distance);
                }
            }
            return new Tuple<bool, float>(false, 0);
        }

        public override Vector3 surface_normal(Vector3 hitPoint)
        {
            return -this.Normal;
        }
    }

    class Ray
    {
        public Vector3 Origin;
        public Vector3 Direction;

        public Ray(Vector3 origin, Vector3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public static Ray createPrime(int x, int y, Scene scene)
        {
            if (scene.Width < scene.Height)
            {
                throw new NotImplementedException("Needs to have a bigger width that height");
            }

            double fovAdjustment = Math.Tan(DegreeToRadian(scene.Fov) / 2);
            double aspectRatio = (float) scene.Width / scene.Height;

            // todo : check this again
            float sensorX = (float) (((x + 0.5) / scene.Width * 2.0 - 1.0) * aspectRatio * fovAdjustment);
            float sensorY = (float) ((1.0 - (y + 0.5) / scene.Height * 2.0) * fovAdjustment);

            return new Ray(Vector3.Zero, Vector3.Normalize(new Vector3()
            {
                X = sensorX,
                Y = sensorY,
                Z = -1
            }));
        }

        public static Tuple<bool, Intersection> Trace(Scene scene, Ray ray, Element current = null)
        {
            float shortest = 30000;
            Intersection shortestIntersection = null;
            foreach (Element element in scene.Elements)
            {
                if(element == current)continue;
                
                var intersects = element.Intersect(ray);
                if (intersects.Item1 && intersects.Item2 < shortest)
                {
                    shortestIntersection = new Intersection(intersects.Item2, element);
                    shortest = intersects.Item2;
                }
            }
            return new Tuple<bool, Intersection>(shortestIntersection != null, shortestIntersection);
        }

        private static double DegreeToRadian(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        private static double RadianToDegree(double angle)
        {
            return angle * (180.0 / Math.PI);
        }
    }

    class Light
    {
        public Vector3 Direction;
        public Color Color;
        public float Intensity;
    }

    class Intersection
    {
        public float Distance;
        public Element Element;

        public Intersection(float distance, Element element)
        {
            Distance = distance;
            Element = element;
        }
    }

    class Scene
    {
        public float shadowBias = 1e-13f;
        public int Width;
        public int Height;
        public float Fov;
        public List<Element> Elements;
        public Light Light;
    }

    class Program
    {
        public void render(Scene scene)
        {
            Bitmap image = new Bitmap(scene.Width, scene.Height);
            Graphics graphics = Graphics.FromImage(image);
            graphics.FillRectangle(Brushes.LightSkyBlue, 0,0,scene.Width,scene.Height);

            int count = 0;

            for (int x = 0; x < scene.Width; x++)
            {
                for (int y = 0; y < scene.Height; y++)
                {
                    var ray = Ray.createPrime(x, y, scene);
                    Tuple<bool, Intersection> trace = Ray.Trace(scene, ray);

                    if (trace.Item1 && trace.Item2 != null)
                    {
                        graphics.FillRectangle(
                            trace.Item2.Element.GetBrush(scene, ray, trace.Item2), x, y, 1, 1);
                    }

                }
            }

            image.Save("test.png", ImageFormat.Png);
        }


        static void Main(string[] args)
        {
            Scene scene = new Scene()
            {
                Width = 800,
                Height = 600,
                Fov = 90,
                Elements = new List<Element>()
                {
                    new Sphere(new Vector3(-1, 0, -3), 1.5f, Color.FromArgb(255, 0, 255, 0)),
                    new Sphere(new Vector3(1, 1, -5), 1, Color.Red),
                    new Plane(new Vector3(0, -2, -5), new Vector3(0, -1, 0), Color.LightGreen)
                },
                Light = new Light()
                {
                    Color = Color.White,
                    Intensity = 2,
//                    Direction = new Vector3(0, 0, -1)
                    Direction = new Vector3(0, -1, 0)
                }
            };

            Program program = new Program();
            program.render(scene);
            Process.Start("test.png");
        }
    }
}