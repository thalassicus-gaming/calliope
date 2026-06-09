// Utilities.cs
// Document Version 0.2.0

using Godot;
using System;
using System.Collections.Generic;
using System.IO;

//
// Toolbox
//
namespace Calliope {

public static class Globals {
	public static Random simulationRandom   = new Random();
	public static Random presentationRandom = new Random();

	public static void SeedSimulation(int seed) {
		simulationRandom = new Random(seed);
	}
  public static Logger log = new Logger(Logger.TRACE);
	public static double m2ft = 0.0328;
	public static double kg2lbs = 2.2;
}

public class Logger {
	public static int NONE  = 0;
	public static int ERROR = 1;
	public static int WARN  = 2;
	public static int INFO  = 3;
	public static int TRACE = 4;

	public int level;
	public string fileName;

	public Logger(int level, string fileName = "") {
		this.level = level;
		this.fileName = fileName;
	}

	private void Log(string prefix, string message, object[] list) {
		string body = String.Format(message, list);
		GD.Print(prefix + body);
		if (fileName != "") {
			using StreamWriter file = new StreamWriter(fileName, append: true);
			file.WriteLine($"{DateTime.Now.TimeOfDay,-16} {prefix}{body}");
		}
	}

	public void Error(string message, params object[] list) {
		if (level >= ERROR) Log("ERROR: ", message, list);
	}
	public void Warn(string message, params object[] list) {
		if (level >= WARN)  Log("WARN:  ", message, list);
	}
	public void Info(string message, params object[] list) {
		if (level >= INFO)  Log("INFO:  ", message, list);
	}
	public void Trace(string message, params object[] list) {
		if (level >= TRACE) Log("TRACE: ", message, list);
	}
}

public static class Tools {
	public static double GetCartesianDistance(double x1, double x2, double y1, double y2){
		return Math.Sqrt(Math.Pow(x1-x2, 2) + Math.Pow(y1-y2, 2));
	}
	public static double GetPolarDistance(double r1, double r2, double t1, double t2){
		return Math.Sqrt(r1*r1 + r2*r2 - 2*r1*r2*Math.Cos(t2-t1));
	}
	public static (double radius, double theta) CartesianToPolar(double x, double y) {
		double radius = Math.Sqrt(x*x + y*y);
		double theta = Math.Atan(y/x);
		if (x < 0) {theta += Math.PI;}
		else if (y < 0) {theta += 2*Math.PI;}
		return (radius, theta);
	}
	public static (double x, double y) PolarToCartesian(double radius, double theta){
		double x = radius * Math.Cos(theta);
		double y = radius * Math.Sin(theta);
		return (x,y);
	}
	public static T GetRandomWeighted<T>(Dictionary<T, double> weights) {
		var totalWeights = new Dictionary<T, double>();

		double totalWeight = 0.0;
		foreach (var weight in weights) {
			if (weight.Value <= 0) continue;
			totalWeight += weight.Value;
			totalWeights.Add(weight.Key, totalWeight);
		}

		double randomTotalWeight = Globals.simulationRandom.NextDouble() * totalWeight;
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default(T);
	}
	public static T GetRandomWeighted<T>(Dictionary<T, int> weights) {
		var totalWeights = new Dictionary<T, int>();

		int totalWeight = 0;
		foreach (var weight in weights) {
			if (weight.Value <= 0) continue;
			totalWeight += weight.Value;
			totalWeights.Add(weight.Key, totalWeight);
		}

		int randomTotalWeight = (int)(Globals.simulationRandom.NextDouble() * (double)totalWeight);
		foreach (var weight in totalWeights) {
			if (weight.Value >= randomTotalWeight) {
				return weight.Key;
			}
		}
		return default(T);
	}
	public static double Constrain(double min, double x, double max) {
		return Math.Max(min, Math.Min(max, x));
	}
	public static int RollDice(int quantity, int sides) {
		int total = 0;
		for (int i = 0; i < quantity; i++) {
			total += Globals.simulationRandom.Next(1, sides + 1);
		}
		return total;
	}
	// Convert an HLS value into an RGB value.
	public static Color HlsToRgb(double h, double l, double s) {
		double p2;
		if (l <= 0.5) p2 = l * (1 + s);
		else p2 = l + s - l * s;

		double p1 = 2 * l - p2;
		double double_r, double_g, double_b;
		if (s == 0) {
			double_r = l;
			double_g = l;
			double_b = l;
		} else {
			double_r = QqhToRgb(p1, p2, h + 120);
			double_g = QqhToRgb(p1, p2, h);
			double_b = QqhToRgb(p1, p2, h - 120);
		}
		return new Color((float)double_r, (float)double_g, (float)double_b);
	}
	private static double QqhToRgb(double q1, double q2, double hue) {
		if (hue > 360) hue -= 360;
		else if (hue < 0) hue += 360;

		if (hue < 60) return q1 + (q2 - q1) * hue / 60;
		if (hue < 180) return q2;
		if (hue < 240) return q1 + (q2 - q1) * (240 - hue) / 60;
		return q1;
	}
}

public abstract class Probability {
	public double min, max, med, range;
	protected double shiftExponent;
	protected bool shifted;
	public abstract double NextDouble();
	public Probability(double min = 0, double max = 1) {
		this.min = min;
		this.max = max;
		this.range = max-min;
		this.med = min + range / 2.0;
		this.shiftExponent = 1;
	}
	public Probability(double min, double med, double max, double? stDev = null, double? filterMin = null, double? filterMax = null) {
		this.min = min;
		this.max = max;
		this.range = max-min;
		this.med = Tools.Constrain(min + 0.01*range, med, max);
		this.shiftExponent = Math.Log((this.med-min)/range, 0.5);
	}
	protected double ShiftResult(double input) {
		var result = min + range * Math.Pow(input, shiftExponent);
		return result;
	}
	public double Percentile(double input) {
		return (input - min) / range;
	}
	public double PercentFromMedian(double input) {
		if (input < med) {
			return Math.Abs(med - input) / (med - min);
		} else {
			return Math.Abs(input - med) / (max - med);
		}
	}
}
public class GaussP : Probability {
	public double filterMin, filterMax, stDev, normDev;
	private double mid, truncateExponent;
	private bool extremeMin, extremeMax;
	public GaussP(double min = 0, double max = 1) : base(min, max) {
		this.filterMin = min;
		this.filterMax = max;
		this.stDev = range/6.0;
		this.normDev = 1.0/6.0;
	}
	public GaussP(double min, double med, double max, double? stDev = null, double? filterMin = null, double? filterMax = null) : base(min, med, max) {
		this.filterMin = filterMin ?? min;
		this.filterMax = filterMax ?? max;
		this.mid = min + range/2.0;
		if (this.filterMin > mid + 2 * stDev) extremeMin = true;
		if (this.filterMax < mid - 2 * stDev) extremeMax = true;
		if (stDev == null){
			this.stDev = range/6.0;
			normDev = 1.0/6.0;
		}else{
			this.stDev = (double)stDev;
			normDev = this.stDev/range;
		}
		truncateExponent = Math.Exp(-0.5 * Math.Pow(0.5/normDev, 2));
	}

	public override double NextDouble() {
		double result;
		if (extremeMin) {
			// extreme filter with >97% reject rate
			if (filterMax != max) max = Math.Min(max, (double)filterMax);
			min = Math.Max(min, (double)filterMin);
			result = min + range * Globals.simulationRandom.NextDouble();
			//GD.Print("Filter override {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
			return result;
		}
		if (extremeMax) {
			// extreme filter with >97% reject rate
			max = Math.Min(max, (double)filterMax);
			if (filterMin != min) min = Math.Max(min, (double)filterMin);
			result = min + range * Globals.simulationRandom.NextDouble();
			//GD.Print("Filter override {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
			return result;
		}
		do {
			result = ShiftResult(NextGaussian());
		} while (result < filterMin || result > filterMax);
		//GD.Print("GetFromGaussP {0}, {1:n2}, {2:n2}, {3:n2}, {4:n2}", result, min, med, max, stDev);
		return result;
	}
	
	protected double NextGaussian() {
		double u1 = Globals.simulationRandom.NextDouble() * (1 - truncateExponent) + truncateExponent;
		double u2 = Globals.simulationRandom.NextDouble();
		double result = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
		result = 0.5 + normDev * result;
		if (double.IsNaN(result)) {
			GD.Print("{0}, {1:n2}, {2:n2}, {3:n2}", result, u1, u2, truncateExponent);
		}
		return result;
	}
}
public class UniformP : Probability {
	public UniformP(double min = 0, double max = 1) : base(min, max) { }
	public UniformP(double min, double med, double max) : base(min, med, max) { }

	public override double NextDouble() {
		return ShiftResult(Globals.simulationRandom.NextDouble());
	}
}
public class DiscreteP : Probability {
	Dictionary<double, int> odds;
	public DiscreteP(Dictionary<double, int> odds, double med = 0.5) : base(0, med, 1) { this.odds = odds; }

	public override double NextDouble() {
		double result = min + range * Globals.simulationRandom.NextDouble();
		if (shifted) result = ShiftResult(result);
		foreach (var pair in odds) {
			if (result <= pair.Key) return pair.Value;
		}
		double lastKey = 0;
		foreach (var key in odds.Keys) lastKey = key;
		return odds[lastKey];
	}
}

}
