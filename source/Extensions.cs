using Open.Numeric;
using Open.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

namespace Open.Collections.Numeric;

public static class Extensions
{
	/// <summary>
	/// Debug utility for asserting if a collection is equal.
	/// </summary>
	public static void AssertEquality<TKey, TValue>(this IDictionary<TKey, TValue> target, IDictionary<TKey, TValue> copy)
		where TValue : IComparable
	{
		if (copy is null && target is null) return;

		if (target is null)
		{
			Debugger.Break();
			Debug.Fail("Target is null.");
			return;
		}

		if (copy is null)
		{
			Debugger.Break();
			Debug.Fail("Copy is null.");
			return;
		}

		if (target.Count != copy.Count)
		{
			Debugger.Break();
			Debug.Fail("Dictionary count mismatch.");
			return;
		}

		if (copy.Keys.Any(key => !target.ContainsKey(key)))
		{
			Debugger.Break();
			Debug.Fail("Copy has key that target doesn't.");
			return;
		}

		foreach (var key in target.Keys)
		{
			if (copy.TryGetValue(key, out TValue? b))
			{
				var a = target[key];
				if (a.IsNearEqual(b, 0.001)) continue;
				Debugger.Break();
				Debug.Fail("Copied value is not equal!");
				return;
			}

			Debugger.Break();
			Debug.Fail("Key missing from copy.");
			return;
		}
	}

	/// <summary>
	/// Creates a single dictionary containing the sum of the values grouped by cacheKey.
	/// </summary>
	/// <param name="values">The source enumerable.</param>
	/// <param name="autoPrecision">True is more accurate but less performant.  False uses default double precision math.</param>
	public static IDictionary<TKey, double> SumValues<TKey>(this IEnumerable<IDictionary<TKey, double>> values, bool autoPrecision = true)
		where TKey : notnull, IComparable
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		Contract.EndContractBlock();

		var result = new ConcurrentDictionary<TKey, double>();
		Action<IDictionary<TKey, double>> f = autoPrecision ? result.AddValuesAccurate : result.AddValues;
		_ = Parallel.ForEach(values, f);
		return result;
	}

	/// <summary>
	/// Creates a single sorted dictionary containing the sum of the values grouped by cacheKey.
	/// </summary>
	/// <param name="values">The source values.</param>
	/// <param name="autoPrecision">True is more accurate but less performant.  False uses default double precision math.</param>
	/// <param name="allowParallel">Enables parallel processing of source enumerable.</param>
	public static SortedDictionary<TKey, double> SumValuesOrdered<TKey>(this IEnumerable<IDictionary<TKey, double>> values, bool autoPrecision = true, bool allowParallel = false)
		where TKey : notnull, IComparable
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		Contract.EndContractBlock();

		var result = new ConcurrentDictionary<TKey, double>();
		values.ForEach(autoPrecision ? result.AddValuesAccurate : result.AddValues, allowParallel);

		return new SortedDictionary<TKey, double>(result);
	}

	/// <summary>
	/// Creates a single sorted dictionary containing the sum of the values grouped by cacheKey.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	/// <param name="autoPrecision">True is more accurate but less performant.  False uses default double precision math.</param>
	public static SortedDictionary<TKey, double> SumValuesOrdered<TKey>(this ParallelQuery<IDictionary<TKey, double>> values, bool autoPrecision = true)
		where TKey : notnull, IComparable
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		Contract.EndContractBlock();

		var result = new ConcurrentDictionary<TKey, double>();
		values.ForAll(autoPrecision ? result.AddValuesAccurate : result.AddValues);

		return new SortedDictionary<TKey, double>(result);
	}

	/// <summary>
	/// Returns how the set of values has changed.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	public static IDictionary<TKey, double> Deltas<TKey>(this IEnumerable<KeyValuePair<TKey, double>> values)
		where TKey : notnull
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		Contract.EndContractBlock();

		var result = new SortedDictionary<TKey, double>();

		double current = 0;
		foreach (var kvp in values.OrderBy(k => k.Key))
		{
			double delta = kvp.Value.SumAccurate(-current); // Must use accurate math otherwise tolerance can throw off entire set.
			result[kvp.Key] = delta;
			current = current.SumAccurate(delta);
		}

		return result;
	}

	/// <summary>
	/// Is the effective inverse of Deltas.  Renders the values as they are based on their changes.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	public static IEnumerable<KeyValuePair<TKey, double>> DeltaCurve<TKey>(this IEnumerable<KeyValuePair<TKey, double>> values)

		where TKey : notnull
	{
		return values is null
			? throw new ArgumentNullException(nameof(values))
			: DeltaCurveCore(values);

		static IEnumerable<KeyValuePair<TKey, double>> DeltaCurveCore(IEnumerable<KeyValuePair<TKey, double>> values)
		{
			Contract.EndContractBlock();

			double current = 0; // Must be done in order...
			foreach (var kv in values.OrderBy(k => k.Key))
			{
				current = current.SumAccurate(kv.Value);
				yield return KeyValuePair.Create(kv.Key, current);
			}
		}
	}

	/// <summary>
	/// Returns how the set of values has changed.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	public static IEnumerable<IDictionary<TKey, double>> Deltas<TKey>(this IEnumerable<IEnumerable<KeyValuePair<TKey, double>>> values)

		where TKey : notnull
		=> values.Select(v => v.Deltas());

	/// <summary>
	/// Returns how the set of values has changed.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	public static ParallelQuery<IDictionary<TKey, double>> Deltas<TKey>(this ParallelQuery<IDictionary<TKey, double>> values)
		where TKey : notnull
		=> values.Select(v => v.Deltas());

	/// <summary>
	/// Accurately adds the values from a set of curves and returns one curve.
	/// </summary>
	/// <param name="values">The source enumerable</param>
	public static IEnumerable<KeyValuePair<TKey, double>> SumCurves<TKey>(this IEnumerable<IDictionary<TKey, double>> values)
		where TKey : notnull, IComparable
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		Contract.EndContractBlock();

		// Optimize to avoiding unnecessary processing...
		var v = values.Memoize();
		var one = v.Take(2).ToArray();

		switch (one.Length)
		{
			case 0:
				v.Dispose();
				return Enumerable.Empty<KeyValuePair<TKey, double>>();
			case 1:
				v.Dispose();
				return new SortedDictionary<TKey, double>(one.Single());
		}

		return v
			.Deltas()
			.SumValuesOrdered()
			.DeltaCurve();
	}

	/// <summary>
	/// Accurately adds the values from a set of curves and returns one curve.
	/// </summary>
	// ReSharper disable once UnusedParameter.Global
	public static IEnumerable<KeyValuePair<TKey, double>> SumCurves<TKey>(this ParallelQuery<IDictionary<TKey, double>> values)
		where TKey : notnull, IComparable
		=> values
			.Deltas()
			.SumValuesOrdered()
			.DeltaCurve();

	/// <summary>
	/// Resets the values to zero if not within the tolerance of zero.
	/// </summary>
	public static IEnumerable<KeyValuePair<TKey, double>> ResetZeros<TKey>(
		this IEnumerable<KeyValuePair<TKey, double>> values, double tolerance = double.Epsilon)
		where TKey : notnull, IComparable
		=> values.Select(v =>
	{
		double value = v.Value;
		return KeyValuePair.Create(v.Key, value.IsNearZero(tolerance) ? 0d : value);
	});

	/// <inheritdoc cref="ResetZeros{TKey}(IEnumerable{KeyValuePair{TKey, double}}, double)"/>/>
	public static ParallelQuery<KeyValuePair<TKey, double>> ResetZeros<TKey>(
		this ParallelQuery<KeyValuePair<TKey, double>> values, double tolerance = double.Epsilon)
		where TKey : notnull, IComparable
		=> values.Select(v =>
	{
		double value = v.Value;
		return KeyValuePair.Create(v.Key, value.IsNearZero(tolerance) ? 0d : value);
	});

	/// <summary>
	/// Divides an existing <typeparamref name="TKey"/> value by the given <paramref name="denominator"/>.
	/// Ignores missing keys.
	/// </summary>
	public static void Divide<TKey>(this IDictionary<TKey, double> target, TKey key, double denominator)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null) throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		/*var c = target as ConcurrentDictionary<TKey, double>;
		//if(c!=null)
		//{
			// No need for locking... (optimistic)*/
		if (target.TryGetValue(key, out double value))
			target[key] = value / denominator;
		/*}
		else
		{
			ThreadSafety.SynchronizeReadWrite(target, key,
				()=>target.ContainsKey(key),
				()=>ThreadSafety.SynchronizeWrite(target, ()=>target[key] /= denominator)
			);
		}*/
	}

	/// <inheritdoc cref="DivideAll{TKey}(IDictionary{TKey, double}, double)"/>
	public static void DivideAll<TKey>(this IDictionary<TKey, double> target, double denominator)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		target.Keys.ToArray().ForEach(
			key => // In this case we get a copy of the keys in order to avoid unsafe enumeration problems.
				Divide(target, key, denominator)
			);
	}

	/// <inheritdoc cref="DivideAll{TKey}(IDictionary{TKey, double}, double)"/>
	public static void DivideAll(this IDictionary<TimeSpan, double> target, double denominator)
		=> DivideAll<TimeSpan>(target, denominator);

	/// <inheritdoc cref="DivideAll{TKey}(IDictionary{TKey, double}, double)"/>
	public static void DivideAll(this IDictionary<DateTime, double> target, double denominator)
		=> DivideAll<DateTime>(target, denominator);

	#region AddValue
	#region ConcurrentDictionary versions

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue<TKey>(this ConcurrentDictionary<TKey, double> target, TKey key, double value)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// Uses a more accurate and less performant method instead of double precision math.
	/// </summary>
	public static void AddValueAccurate<TKey>(this ConcurrentDictionary<TKey, double> target, TKey key, double value)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old.SumAccurate(value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue<TKey>(this ConcurrentDictionary<TKey, int> target, TKey key, int value)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue<TKey>(this ConcurrentDictionary<TKey, uint> target, TKey key, uint value)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void IncrementValue<TKey>(this ConcurrentDictionary<TKey, uint> target, TKey key)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, 1, (_, old) => old + 1);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue(this ConcurrentDictionary<TimeSpan, double> target, TimeSpan time, double value)
		=> AddValue<TimeSpan>(target, time, value);

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue(this ConcurrentDictionary<TimeSpan, double> target, DateTime datetime, double value)
		=> AddValue(target, datetime.TimeOfDay, value);

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValue(this ConcurrentDictionary<DateTime, double> target, TimeSpan time, double v)
		=> AddValue(target, DateTime.MinValue.Add(time), v);

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// </summary>
	public static void AddValues<TKey>(this ConcurrentDictionary<TKey, double> target, IDictionary<TKey, double> add, bool allowParallel = false)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (add is null)
			throw new ArgumentNullException(nameof(add));
		Contract.EndContractBlock();

		add.Keys.ForEach(key => AddValue(target, key, add[key]), allowParallel);
	}

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// </summary>
	public static void AddValues<TKey>(this IDictionary<TKey, double> target, IDictionary<TKey, double> add)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (add is null)
			throw new ArgumentNullException(nameof(add));
		Contract.EndContractBlock();

		// For abs peak performance only create locking for individual entries...
		foreach (var key in add.Keys)
		{
			AddValue(target, key, add[key]);
		}
	}

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// Uses a more accurate and less performant method instead of double precision math.
	/// </summary>
	public static void AddValuesAccurateSelective<TKey>(
		this ConcurrentDictionary<TKey, double> target, IDictionary<TKey, double> add, bool allowParallel = false)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (add is null)
			throw new ArgumentNullException(nameof(add));
		Contract.EndContractBlock();

		add.Keys.ForEach(key => AddValueAccurate(target, key, add[key]), allowParallel);
	}

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// Uses a more accurate and less performant method instead of double precision math.
	/// </summary>
	public static void AddValuesAccurate<TKey>(this ConcurrentDictionary<TKey, double> target, IDictionary<TKey, double> add)
		where TKey : notnull
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (add is null)
			throw new ArgumentNullException(nameof(add));
		Contract.EndContractBlock();

		AddValuesAccurateSelective(target, add);
	}

	#endregion

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// Uses a accurate and less performant method instead of double precision math.
	/// NOT THREAD SAFE: Use only when a dictionary local or is assured single threaded.
	/// </summary>
	public static void AddValueAccurate<TKey>(this IDictionary<TKey, double> target, TKey key, double value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old.SumAccurate(value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// NOT THREAD SAFE: Use only when a dictionary local or is assured single threaded.
	/// </summary>
	public static void AddValue<TKey>(this IDictionary<TKey, double> target, TKey key, double value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// NOT THREAD SAFE: Use only when a dictionary local or is assured single threaded.
	/// </summary>
	public static void AddValue<TKey>(this IDictionary<TKey, int> target, TKey key, int value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// NOT THREAD SAFE: Use only when a dictionary local or is assured single threaded.
	/// </summary>
	public static void AddValue<TKey>(this IDictionary<TKey, uint> target, TKey key, uint value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = target.AddOrUpdate(key, value, (_, old) => old + value);
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// Uses a more accurate and less performant method instead of double precision math.
	/// </summary>
	public static void AddValueAccurateSynchronized<TKey>(this IDictionary<TKey, double> target, TKey key, double value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = ThreadSafety.SynchronizeWrite(target, () => target.AddValueAccurate(key, value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized<TKey>(this IDictionary<TKey, double> target, TKey key, double value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = ThreadSafety.SynchronizeWrite(target, () => target.AddValue(key, value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized<TKey>(this IDictionary<TKey, int> target, TKey key, int value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = ThreadSafety.SynchronizeWrite(target, () => target.AddValue(key, value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized<TKey>(this IDictionary<TKey, uint> target, TKey key, uint value)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		if (key is null)
			throw new ArgumentNullException(nameof(key));
		Contract.EndContractBlock();

		_ = ThreadSafety.SynchronizeWrite(target, () => target.AddValue(key, value));
	}

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void IncrementValueSynchronized<TKey>(this IDictionary<TKey, uint> target, TKey key)
		=> target.AddValueSynchronized(key, 1);

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized(this IDictionary<TimeSpan, double> target, DateTime datetime, double value)
		=> target.AddValueSynchronized(datetime.TimeOfDay, value);

	/// <summary>
	/// Adds a value to the colleciton or replaces the existing value with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized(this IDictionary<DateTime, double> target, TimeSpan time, double value)
		=> target.AddValueSynchronized(DateTime.MinValue.Add(time), value);

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// </summary>
	public static void AddValueSynchronized<TKey>(this IDictionary<TKey, double> target, IDictionary<TKey, double> add)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		// For abs peak performance only create locking for individual entries...
		_ = Parallel.ForEach(add, kv => target.AddValueSynchronized(kv.Key, kv.Value));
	}

	/// <summary>
	/// Adds values to the colleciton or replaces the existing values with the sum of the two.
	/// Uses a more accurate and less performant method instead of double precision math.
	/// </summary>
	public static void AddValueAccurateSynchronized<TKey>(this IDictionary<TKey, double> target, IDictionary<TKey, double> add)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		// For abs peak performance only create locking for individual entries...
		_ = Parallel.ForEach(add, kv => target.AddValueAccurateSynchronized<TKey>(kv.Key, kv.Value));
	}
	#endregion

}
