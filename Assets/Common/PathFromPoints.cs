using System.Globalization;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointsFromData {
	private TextAsset assetDataPoints;
	public List<List<Vector3>> PathsToPoints = new List<List<Vector3>>();
	public List<Vector3> Points = new List<Vector3>();
	private float Scale = 0.03f;
	private Vector3 InitialPosition;
	private bool ReverseYAxis = true;
	private Vector3 RotationAngles;

	/// <summary>
    /// Init points from file
    /// </summary>
    /// <param name="assetDataPoints">Asset file containing path of points (see https://shinao.github.io/PathToPoints/)</param>
    /// <param name="initialPosition">Initial position of all the branches</param>
	public void GeneratePointsFrom(TextAsset assetDataPoints, Vector3 initialPosition, Vector3 rotationAngles, bool reverseYAxis = true, float scale = 0.03f) {
		Scale = scale;
		RotationAngles = rotationAngles;
		ReverseYAxis = reverseYAxis;
		InitialPosition = initialPosition;
		PathsToPoints.Clear();

		Vector2 minValue = new Vector2(float.MaxValue, float.MaxValue);
		Vector2 maxValue = new Vector2(float.MinValue, float.MinValue);

		 var str_path_points = assetDataPoints.text;
		 var str_paths = str_path_points.Split(new char[] { '#' }, System.StringSplitOptions.RemoveEmptyEntries).Where(branch => branch.Count() > 2).ToArray();

		 foreach (var str_path in str_paths)
		 {
			 var data_points = new List<Vector3>();
			 var lines_points = str_path.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries).Where(line_point => line_point.Count() > 2).ToArray();;
			 foreach (var line_point in lines_points)
			 {
				var point = line_point.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
				var x = float.Parse(point[0], CultureInfo.InvariantCulture);
				var y = float.Parse(point[1], CultureInfo.InvariantCulture);

				data_points.Add(new Vector3(x, y, 0.0f));
			 }

			 minValue.y = Mathf.Min(minValue.y, data_points.Select(v => v.y).Min());
			 maxValue.y = Mathf.Max(maxValue.y, data_points.Select(v => v.y).Max());
			 minValue.x = Mathf.Min(minValue.x, data_points.Select(v => v.x).Min());
			 maxValue.x = Mathf.Max(maxValue.x, data_points.Select(v => v.x).Max());

			 PathsToPoints.Add(data_points);
		 }

		ScaleAndReposition(minValue, maxValue);
	}

	/// <summary>
    /// Scale and reposition path depending on min/max and target value
    /// </summary>
    /// <param name="minValue">Min x,y value of all paths points</param>
    /// <param name="maxValue">Max x,y value of all paths points</param>
	private void ScaleAndReposition(Vector2 minValue, Vector2 maxValue)
	{
		var xSum = PathsToPoints.Sum(ptp => ptp.Sum(p => p.x));
		var ySum = PathsToPoints.Sum(ptp => ptp.Sum(p => p.y));
		var nbPoints = PathsToPoints.Sum(ptp => ptp.Count());
		var center = new Vector2(xSum / nbPoints, ySum / nbPoints);
		
		foreach (var pathToPoints in PathsToPoints) {
			for (int idx = 0; idx < pathToPoints.Count(); ++idx)
			{
				var newPoint = pathToPoints[idx];
				if (ReverseYAxis)
				{
					newPoint = new Vector3(newPoint.x, newPoint.y * -1.0f, newPoint.z);
					newPoint = newPoint + new Vector3(0.0f, minValue.y + maxValue.y, 0.0f);
				}
				newPoint = RotatePointAroundPivot(newPoint, center, RotationAngles);
				newPoint = newPoint - new Vector3(minValue.x, minValue.y, 0.0f) - new Vector3(center.x, center.y, 0.0f);
				newPoint = newPoint * Scale;
				newPoint = newPoint + InitialPosition;
				
				pathToPoints[idx] = newPoint;
			}
		}

		Points = PathsToPoints.SelectMany(x => x).ToList();		
	}

	public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles) {
         return RotatePointAroundPivot(point, pivot, Quaternion.Euler(angles));
     }
 
     public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation) {
         return rotation * (point - pivot) + pivot;
     }
}