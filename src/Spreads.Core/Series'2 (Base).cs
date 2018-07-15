﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Threading;
using Spreads.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads
{
    /// <summary>
    /// Base class for series implementations.
    /// </summary>
    [CannotApplyEqualityOperator]
    public class BaseSeries
    {
        private static readonly ConditionalWeakTable<BaseSeries, Dictionary<string, object>> Attributes =
            new ConditionalWeakTable<BaseSeries, Dictionary<string, object>>();

        /// <summary>
        /// Get an attribute that was set using SetAttribute() method.
        /// </summary>
        /// <param name="attributeName">Name of an attribute.</param>
        /// <returns>Return an attribute value or null is the attribute is not found.</returns>
        public object GetAttribute(string attributeName)
        {
            if (Attributes.TryGetValue(this, out Dictionary<string, object> dic) &&
                dic.TryGetValue(attributeName, out object res))
            {
                return res;
            }
            return null;
        }

        /// <summary>
        /// Set any custom attribute to a series. An attribute is available during lifetime of a series and is available via GetAttribute() method.
        /// </summary>
        public void SetAttribute(string attributeName, object attributeValue)
        {
            var dic = Attributes.GetOrCreateValue(this);
            dic[attributeName] = attributeValue;
        }
    }

    /// <summary>
    /// Base generic class for all series implementations.
    /// </summary>
    /// <typeparam name="TKey">Type of series keys.</typeparam>
    /// <typeparam name="TValue">Type of series values.</typeparam>
#pragma warning disable 660, 661

    public abstract class Series<TKey, TValue> : BaseSeries,
        ISpecializedSeries<TKey, TValue, Cursor<TKey, TValue>>
#pragma warning restore 660, 661
    {
        /// <inheritdoc />
        public abstract ICursor<TKey, TValue> GetCursor();

        /// <inheritdoc />
        public abstract KeyComparer<TKey> Comparer { get; }

        /// <inheritdoc />
        public abstract bool IsIndexed { get; }

        /// <inheritdoc />
        public abstract bool IsCompleted { get; }

        public abstract IAsyncEnumerator<KeyValuePair<TKey, TValue>> GetAsyncEnumerator();

        public abstract IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        Cursor<TKey, TValue> ISpecializedSeries<TKey, TValue, Cursor<TKey, TValue>>.GetCursor()
        {
            return GetWrapper();
        }

        /// <inheritdoc />
        public abstract Opt<KeyValuePair<TKey, TValue>> First { get; }

        /// <inheritdoc />
        public abstract Opt<KeyValuePair<TKey, TValue>> Last { get; }

        /// <inheritdoc />
        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                ThrowHelper.ThrowKeyNotFoundException("Key not found in series");
                return default;
            }
        }

        /// <inheritdoc />
        public abstract bool TryGetValue(TKey key, out TValue value);

        /// <inheritdoc />
        public abstract bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp);

        /// <inheritdoc />
        public abstract bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp);

        /// <inheritdoc />
        public virtual IEnumerable<TKey> Keys => this.Select(kvp => kvp.Key);

        /// <inheritdoc />
        public virtual IEnumerable<TValue> Values => this.Select(kvp => kvp.Value);

        internal Cursor<TKey, TValue> GetWrapper()
        {
            return new Cursor<TKey, TValue>(GetCursor());
        }

        #region Implicit cast

        /// <summary>
        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(Series<TKey, TValue> series)
        {
            var c = series.GetWrapper();
            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
        }

        #endregion Implicit cast

        #region Unary Operators

        // UNARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>> operator
            +(TValue constant, Series<TKey, TValue> series)
        {
            // Addition is commutative
            var cursor = new Op<TKey, TValue, AddOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>> operator
            -(Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, NegateOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>> operator
            +(Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, PlusOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>> operator
            -(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            -(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, SubtractReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>> operator
            *(TValue constant, Series<TKey, TValue> series)
        {
            // Multiplication is commutative
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>> operator
            /(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, DivideOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            /(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, DivideReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>> operator
            %(Series<TKey, TValue> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>> operator
            %(TValue constant, Series<TKey, TValue> series)
        {
            var cursor =
                new Op<TKey, TValue, ModuloReverseOp<TValue>, Cursor<TKey, TValue>>(series.GetWrapper(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            ==(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            !=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            <=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator >=(
            Series<TKey, TValue> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, Cursor<TKey, TValue>>> operator
            >=(TValue comparand, Series<TKey, TValue> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, Cursor<TKey, TValue>>(series.GetWrapper(), comparand,
                    GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            // TODO change to Op2, measure result
            //var op = zipCursor.Apply<AddOp<TValue>>();
            //return op;
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            +(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            -(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = SubtractOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            *(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = MultiplyOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            /(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = DivideOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            %(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();
            Func<TKey, (TValue, TValue), TValue> selector = ModuloOp<TValue>.ZipSelector;

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(selector).Source;
        }

        // BINARY LOGIC

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            ==(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(EQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            !=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(NEQOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >=(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GEOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            <(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(LTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue> series, Series<TKey, TValue> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetWrapper();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue, Cursor<TKey, TValue>> series, Series<TKey, TValue> other)
        {
            if (other is null) throw new ArgumentNullException(nameof(other));
            var c1 = series.GetEnumerator();
            var c2 = other.GetWrapper();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Map<TKey, (TValue, TValue), bool,
                Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>>> operator
            >(Series<TKey, TValue> series, Series<TKey, TValue, Cursor<TKey, TValue>> other)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var c1 = series.GetWrapper();
            var c2 = other.GetEnumerator();

            var zipCursor = new Zip<TKey, TValue, TValue, Cursor<TKey, TValue>, Cursor<TKey, TValue>>(c1, c2);
            return zipCursor.Map(GTOp<TValue>.ZipSelector).Source;
        }

        #endregion Binary Operators
    }

    /// <summary>
    /// Base class for collections (containers).
    /// </summary>
#pragma warning disable 660, 661

    public abstract class ContainerSeries<TKey, TValue, TCursor> : Series<TKey, TValue>,
        ISpecializedSeries<TKey, TValue, TCursor>, IAsyncCompleter
#pragma warning restore 660, 661
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
    {
        private object _syncRoot;

        // ReSharper disable InconsistentNaming
        internal long _version;

        internal long _nextVersion;

        // ReSharper restore InconsistentNaming
        internal bool _isSynchronized = true;

        internal bool _isReadOnly;

        // Unition of ContainerSubscription | ConcurrentHashSet<ContainerSubscription>
        private object _cursors;

        internal long Locker;

        protected ContainerSeries()
        {
            _doNotifyUpdateSingleSyncCallback = DoNotifyUpdateSingleSync;
        }

        internal abstract TCursor GetContainerCursor();

        /// <inheritdoc />
        public override bool IsCompleted
        {
            // NB this is set only inside write lock, no other locks are possible
            // after this value is set so we do not need read lock. This is very
            // hot path for MNA
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return Volatile.Read(ref _isReadOnly); }
        }

        public sealed override IAsyncEnumerator<KeyValuePair<TKey, TValue>> GetAsyncEnumerator()
        {
            if (IsCompleted)
            {
                return GetContainerCursor();
            }
            var c = new AsyncCursor<TKey, TValue, TCursor>(GetContainerCursor(), true);
            return c;
        }

        public sealed override IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return GetContainerCursor();
        }

        public override ICursor<TKey, TValue> GetCursor()
        {
            if (IsCompleted)
            {
                return GetContainerCursor();
            }
            // NB subscribe from AsyncCursor
            var c = new AsyncCursor<TKey, TValue, TCursor>(GetContainerCursor());
            return c;
        }

        private class ContainerSubscription : IAsyncSubscription
        {
            private readonly ContainerSeries<TKey, TValue, TCursor> _container;
            public readonly WeakReference<IAsyncCompletable> Wr;

            // Public interface exposes only IDisposable, only if subscription is IAsyncSubscription cursor knows what to do
            // Otherwise this number will stay at 1 and NotifyUpdate will send all updates
            private long _requests;

            public long Requests
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return Volatile.Read(ref _requests); }
                // set { Volatile.Write(ref _requests, value); }
            }

            public ContainerSubscription(ContainerSeries<TKey, TValue, TCursor> container, WeakReference<IAsyncCompletable> wr)
            {
                _container = container;
                Wr = wr;
            }

            // ReSharper disable once UnusedParameter.Local
            public void Dispose(bool disposing)
            {
                try
                {
                    Volatile.Write(ref _requests, 0);
                    var existing = Interlocked.CompareExchange(ref _container._cursors, null, this);
                    if (existing == this)
                    {
                        return;
                    }
                    if (existing is HashSet<ContainerSubscription> hashSet)
                    {
                        lock (hashSet)
                        {
                            hashSet.Remove(this);
                        }
                    }
                    else
                    {
                        var message = "Wrong cursors type";
                        Trace.TraceError(message);
                        ThrowHelper.FailFast(message);
                    }
                }
                catch (Exception ex)
                {
                    var message = "Error in unsubscribe: " + ex;
                    Trace.TraceError(message);
                    ThrowHelper.FailFast(message);
                    throw;
                }
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);
                Dispose(true);
            }

            public void RequestNotification(int count)
            {
                Interlocked.Add(ref _requests, count);
            }

            ~ContainerSubscription()
            {
                Dispose(false);
            }
        }

        public IDisposable Subscribe(IAsyncCompletable subscriber)
        {
            var wr = new WeakReference<IAsyncCompletable>(subscriber);
            var subscription = new ContainerSubscription(this, wr);
            try
            {
                while (true)
                {
                    var existing1 = Interlocked.CompareExchange<object>(ref _cursors, subscription, null);
                    if (existing1 == null)
                    {
                        break;
                    }

                    if (existing1 is HashSet<ContainerSubscription> hashSet)
                    {
                        lock (hashSet)
                        {
                            if (hashSet.Contains(subscription))
                            {
                                // NB not failfast, existing are not affected
                                ThrowHelper.ThrowInvalidOperationException("Already subscribed");
                            }
                            hashSet.Add(subscription);
                        }

                        break;
                    }

                    if (!(existing1 is ContainerSubscription existing2))
                    {
                        ThrowHelper.FailFast("Wrong _cursors type.");
                        return default;
                    }
                    var newHashSet = new HashSet<ContainerSubscription>();
                    if (existing2.Wr.TryGetTarget(out _))
                    {
                        newHashSet.Add(existing2);
                    }
                    // ReSharper disable once RedundantIfElseBlock
                    else
                    {
                        // No need for existing2.Dispose(), it will be GCed, save one recursive call -
                        // otherwise Dispose will set _cursors to null vs existing2:
                    }

                    if (newHashSet.Contains(subscription))
                    {
                        // NB not failfast, existing are not affected
                        ThrowHelper.ThrowInvalidOperationException("Already subscribed");
                    }
                    newHashSet.Add(subscription);
                    var existing3 = Interlocked.CompareExchange<object>(ref _cursors, newHashSet, existing2);
                    if (existing3 == existing2)
                    {
                        break;
                    }
                }
                return subscription;
            }
            catch (Exception ex)
            {
                var message = "Error in ContainerSeries.Subscribe: " + ex;
                Trace.TraceError(message);
                ThrowHelper.FailFast(message);
                throw;
            }
        }

        TCursor ISpecializedSeries<TKey, TValue, TCursor>.GetCursor()
        {
            return GetContainerCursor();
        }

        public override bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp)
        {
            if (idx < 0)
            {
                ThrowHelper.ThrowNotImplementedException("TODO Support negative indexes in TryGetAt");
            }
            // TODO (review) not so stupid and potentially throwing impl
            try
            {
                kvp = this.Skip(Math.Max(0, checked((int)(idx)) - 1)).First();
                return true;
            }
            catch
            {
                kvp = default;
                return false;
            }
        }

        #region Synchronization

        /// <summary>
        /// An object for external synchronization.
        /// </summary>
        public object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        /// <summary>
        /// Takes a write lock, increments _nextVersion field and returns the current value of the _version field.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void BeforeWrite()
        {
            // TODO review (recall) why SCM needed access to this

#if DEBUG
            var sw = new Stopwatch();
            sw.Start();
#endif
            var spinwait = new SpinWait();
            // NB try{} finally{ .. code here .. } prevents method inlining, therefore should be used at the caller place, not here
            var doSpin = _isSynchronized;
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
            while (doSpin)
            {
                if (Interlocked.CompareExchange(ref Locker, 1L, 0L) == 0L)
                {
                    if (IntPtr.Size == 8)
                    {
                        Volatile.Write(ref _nextVersion, _nextVersion + 1L);
                        // see the aeron.net 49 & related coreclr issues
                        _nextVersion = Volatile.Read(ref _nextVersion);
                    }
                    else
                    {
                        Interlocked.Increment(ref _nextVersion);
                    }

                    // do not return from a loop, see CoreClr #9692
                    break;
                }
#if DEBUG
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                if (elapsed > 1000)
                {
                    TryUnlock();
                }
#endif
                spinwait.SpinOnce();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Conditional("DEBUG")]
        internal virtual void TryUnlock()
        {
            ThrowHelper.FailFast("This should never happen. Locks are only in memory and should not take longer than a millisecond.");
        }

        /// <summary>
        /// Release write lock and increment _version field or decrement _nextVersion field if no updates were made.
        /// Call NotifyUpdate if doVersionIncrement is true
        /// </summary>
        /// <param name="doVersionIncrement"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("Use this ONLY for IMutableSeries operations")]
        internal void AfterWrite(bool doVersionIncrement)
        {
            if (!_isSynchronized)
            {
                if (doVersionIncrement)
                {
                    _version++;
                    _nextVersion = _version;
                }
            }
            else if (doVersionIncrement)
            {
                if (IntPtr.Size == 8)
                {
                    Volatile.Write(ref _version, _version + 1L);
                }
                else
                {
                    Interlocked.Increment(ref _version);
                }

                NotifyUpdate(false);
            }
            else
            {
                // set nextVersion back to original version, no changes were made
                if (IntPtr.Size == 8)
                {
                    Volatile.Write(ref _nextVersion, _version);
                }
                else
                {
                    Interlocked.Exchange(ref _nextVersion, _version);
                }
            }

            // release write lock
            if (IntPtr.Size == 8)
            {
                Volatile.Write(ref Locker, 0L);
            }
            else
            {
                Interlocked.Exchange(ref Locker, 0L);
            }
        }

        [Obsolete("This cannot be inlined. In F# use inline function, in C# have to do manually on hot paths.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected T ReadLock<T>(Func<T> f)
        {
            T value;
            var doSpin = _isSynchronized;
            SpinWait sw = default;
            do
            {
                var version = doSpin ? Volatile.Read(ref _version) : 0L;
                value = f.Invoke();
                if (!doSpin) { break; }
                var nextVersion = Volatile.Read(ref _nextVersion);
                if (version == nextVersion) { break; }
                sw.SpinOnce();
            } while (true);
            return value;
        }

        protected virtual void Dispose(bool disposing)
        { }

        internal Task DoComplete()
        {
#pragma warning disable 618
            BeforeWrite();
#pragma warning restore 618
            if (!_isReadOnly)
            {
                _isReadOnly = true;
                _isSynchronized = false;
                // NB this is API design quirk: AfterWrite checks for _isSynchronized
                // and ignores all further logic if that is false, but BeforeWrite
                // always increments _nextVersion when _isSynchronized = true
                // We have it only here, ok for now but TODO review later
                _nextVersion = _version;
            }
#pragma warning disable 618
            AfterWrite(false);
#pragma warning restore 618
            Interlocked.Exchange(ref Locker, 0L);
            NotifyUpdate(true);
            return Task.CompletedTask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void NotifyUpdate(bool force)
        {
            var cursors = _cursors;
            if (cursors != null)
            {
                DoNotifyUpdate(force);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DoNotifyUpdate(bool force)
        {
            var cursors = _cursors;

            if (cursors is ContainerSubscription sub)
            {
                if ((sub.Requests > 0 || force) && sub.Wr.TryGetTarget(out var tg))
                {
                    // ReSharper disable once InconsistentlySynchronizedField
                    SpreadsThreadPool.Default.UnsafeQueueCompletableItem(_doNotifyUpdateSingleSyncCallback, tg, true);
                }
            }
            else if (cursors is HashSet<ContainerSubscription> hashSet)
            {
                lock (hashSet)
                {
                    foreach (var kvp in hashSet)
                    {
                        var sub1 = kvp;
                        if ((sub1.Requests > 0 || force) && sub1.Wr.TryGetTarget(out var tg))
                        {
                            SpreadsThreadPool.Default.UnsafeQueueCompletableItem(_doNotifyUpdateSingleSyncCallback, tg, true);
                        }
                    }
                }
            }
            else
            {
                ThrowHelper.FailFast("Wrong cursors subscriptions type");
            }
        }

        private readonly Action<object> _doNotifyUpdateSingleSyncCallback;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DoNotifyUpdateSingleSync(object obj)
        {
            var cursor = (IAsyncCompletable)obj;
            cursor.TryComplete(false, false);
        }

        #endregion Synchronization

        #region Unary Operators

        // UNARY ARITHMETIC

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Add operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, AddOp<TValue>, TCursor>> operator
            +(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            // Addition is commutative
            var cursor = new Op<TKey, TValue, AddOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Negate operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, NegateOp<TValue>, TCursor>> operator
            -(ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, NegateOp<TValue>, TCursor>(series.GetContainerCursor(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Unary plus operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, PlusOp<TValue>, TCursor>> operator
            +(ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, PlusOp<TValue>, TCursor>(series.GetContainerCursor(), default(TValue));
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractOp<TValue>, TCursor>> operator
            -(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, SubtractOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Subtract operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>> operator
            -(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor =
                new Op<TKey, TValue, SubtractReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Multiply operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, MultiplyOp<TValue>, TCursor>> operator
            *(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            // Multiplication is commutative
            var cursor = new Op<TKey, TValue, MultiplyOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideOp<TValue>, TCursor>> operator
            /(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, DivideOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Divide operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>> operator
            /(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, DivideReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloOp<TValue>, TCursor>> operator
            %(ContainerSeries<TKey, TValue, TCursor> series, TValue constant)
        {
            var cursor = new Op<TKey, TValue, ModuloOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        /// <summary>
        /// Modulo operator.
        /// </summary>
        public static Series<TKey, TValue, Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>> operator
            %(TValue constant, ContainerSeries<TKey, TValue, TCursor> series)
        {
            var cursor = new Op<TKey, TValue, ModuloReverseOp<TValue>, TCursor>(series.GetContainerCursor(), constant);
            return cursor.Source;
        }

        // UNARY LOGIC

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values equal operator. Use ReferenceEquals or SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            ==(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, EQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Values not equal operator. Use !ReferenceEquals or !SequenceEquals for other cases.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            !=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, NEQOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                LTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GTOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                GTReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, LEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            <=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                LEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >=(ContainerSeries<TKey, TValue, TCursor> series, TValue comparand)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor =
                new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand, GEOp<TValue>.Instance);
            return cursor.Source;
        }

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public static Series<TKey, bool, Comparison<TKey, TValue, TCursor>> operator
            >=(TValue comparand, ContainerSeries<TKey, TValue, TCursor> series)
        {
            if (series is null) throw new ArgumentNullException(nameof(series));
            var cursor = new Comparison<TKey, TValue, TCursor>(series.GetContainerCursor(), comparand,
                GEReverseOp<TValue>.Instance);
            return cursor.Source;
        }

        #endregion Unary Operators

        #region Binary Operators

        // BINARY ARITHMETIC

        ///// <summary>
        ///// Add operator.
        ///// </summary>
        //public static Series<TKey, TValue, Map<TKey, (TValue, TValue), TValue, Zip<TKey, TValue, TValue, TCursor, TCursor>>> operator
        //    +(ContainerSeries<TKey, TValue, TCursor> series, ContainerSeries<TKey, TValue, TCursor> other)
        //{
        //    var c1 = series.GetContainerCursor();
        //    var c2 = other.GetContainerCursor();
        //    //Func<TKey, (TValue, TValue), TValue> selector = AddOp<TValue>.ZipSelector;

        //    var zipCursor = new Zip<TKey, TValue, TValue, TCursor, TCursor>(c1, c2);
        //    return zipCursor.Map(AddOp<TValue>.ZipSelector).Source;

        //    //var op2 = new Op2<TKey, TValue, AddOp<TValue>, Zip<TKey, TValue, TValue, TCursor, TCursor>>(zipCursor);

        //    //return op2.Source;
        //}

        #endregion Binary Operators

        #region Implicit cast

        /// <summary>
        /// Implicitly convert <see cref="Series{TKey,TValue}"/> to <see cref="Series{TKey,TValue,TCursor}"/>
        /// using <see cref="Cursor{TKey,TValue}"/> wrapper.
        /// </summary>
        public static implicit operator Series<TKey, TValue, Cursor<TKey, TValue>>(
            ContainerSeries<TKey, TValue, TCursor> series)
        {
            var c = series.GetWrapper();
            return new Series<TKey, TValue, Cursor<TKey, TValue>>(c);
        }

        #endregion Implicit cast
    }

    /// <summary>
    /// Base class for collections (containers) with <see cref="ISeries{TKey,TValue}"/> members implemented via a cursor.
    /// </summary>
    public abstract class CursorContainerSeries<TKey, TValue, TCursor> : ContainerSeries<TKey, TValue, TCursor>, IMutableSeries<TKey, TValue>, IDisposable
#pragma warning restore 660, 661
        where TCursor : ISpecializedCursor<TKey, TValue, TCursor>
#pragma warning restore 660, 661
    {
        private bool _cursorIsSet;
        private TCursor _c;

        private TCursor C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                lock (SyncRoot)
                {
                    if (!_cursorIsSet)
                    {
                        _c = GetContainerCursor();
                        _cursorIsSet = true;
                    }

                    return _c;
                }
            }
        }

        #region ISeries members

        /// <inheritdoc />
        public override Opt<KeyValuePair<TKey, TValue>> First
        {
            get
            {
                lock (SyncRoot)
                {
                    return C.MoveFirst() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        /// <inheritdoc />
        public override Opt<KeyValuePair<TKey, TValue>> Last
        {
            get
            {
                lock (SyncRoot)
                {
                    return C.MoveLast() ? Opt.Present(C.Current) : Opt<KeyValuePair<TKey, TValue>>.Missing;
                }
            }
        }

        public override bool TryGetValue(TKey key, out TValue value)
        {
            lock (SyncRoot)
            {
                return C.TryGetValue(key, out value);
            }
        }

        /// <inheritdoc />
        public override bool TryGetAt(long idx, out KeyValuePair<TKey, TValue> kvp)
        {
            // NB call to this.NavCursor.Source.TryGetAt(idx) is recursive (=> SO) and is logically wrong
            if (idx < 0) throw new ArgumentOutOfRangeException(nameof(idx));
            lock (SyncRoot)
            {
                if (!C.MoveFirst())
                {
                    throw new KeyNotFoundException();
                }
                for (var i = 0; i < idx - 1; i++)
                {
                    if (!C.MoveNext())
                    {
                        kvp = default;
                        return false;
                    }
                }
                kvp = C.Current;
                return true;
            }
        }

        /// <inheritdoc />
        public override bool TryFindAt(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> kvp)
        {
            lock (SyncRoot)
            {
                if (C.MoveAt(key, direction))
                {
                    kvp = C.Current;
                    return true;
                }

                kvp = default;
                return false;
            }
        }

        /// <inheritdoc />
        public override IEnumerable<TKey> Keys
        {
            get
            {
                lock (SyncRoot)
                {
                    while (C.MoveNext())
                    {
                        yield return C.CurrentKey;
                    }
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerable<TValue> Values
        {
            get
            {
                lock (SyncRoot)
                {
                    while (C.MoveNext())
                    {
                        yield return C.CurrentValue;
                    }
                }
            }
        }

        #endregion ISeries members

        protected override void Dispose(bool disposing)
        {
            lock (SyncRoot)
            {
                if (!_cursorIsSet)
                {
                    return;
                }
                _cursorIsSet = false;
                _c.Dispose();
            }
            base.Dispose(disposing);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        ~CursorContainerSeries()
        {
            Dispose(false);
        }

        public abstract long Count { get; }

        public abstract long Version { get; }

        public abstract bool IsAppendOnly { get; }

        public abstract Task<bool> Set(TKey key, TValue value);

        public abstract Task<bool> TryAdd(TKey key, TValue value);

        public virtual Task<bool> TryAddLast(TKey key, TValue value)
        {
            lock (SyncRoot)
            {
                if (Last.IsMissing || Comparer.Compare(key, Last.Present.Key) > 0)
                {
                    return TryAdd(key, value);
                }

                return TaskUtil.FalseTask;
            }
        }

        public virtual Task<bool> TryAddFirst(TKey key, TValue value)
        {
            lock (SyncRoot)
            {
                if (First.IsMissing || Comparer.Compare(key, First.Present.Key) < 0)
                {
                    return TryAdd(key, value);
                }

                return TaskUtil.FalseTask;
            }
        }

        public abstract ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveMany(TKey key, Lookup direction);

        public virtual async ValueTask<Opt<TValue>> TryRemove(TKey key)
        {
            var result = await TryRemoveMany(key, Lookup.EQ);
            return result.IsMissing ? Opt<TValue>.Missing : Opt.Present(result.Present.Value);
        }

        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveFirst()
        {
            lock (SyncRoot)
            {
                if (First.IsPresent)
                {
                    return TryRemoveMany(First.Present.Key, Lookup.LE);
                }

                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
            }
        }

        public virtual ValueTask<Opt<KeyValuePair<TKey, TValue>>> TryRemoveLast()
        {
            lock (SyncRoot)
            {
                if (Last.IsPresent)
                {
                    return TryRemoveMany(Last.Present.Key, Lookup.GE);
                }

                return new ValueTask<Opt<KeyValuePair<TKey, TValue>>>(Opt<KeyValuePair<TKey, TValue>>.Missing);
            }
        }

        public abstract Task<bool> TryRemoveMany(TKey key, TValue updatedAtKey, Lookup direction);

        public abstract ValueTask<long> TryAppend(ISeries<TKey, TValue> appendMap, AppendOption option = AppendOption.RejectOnOverlap);

        public abstract Task Complete();
    }
}