package com.romaskull.neonhorizon;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Shader;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.View;

public final class NeonSliderView extends View {
    public interface OnValueChangeListener {
        void onValueChanged(NeonSliderView slider, int value, boolean committed);
    }

    private final Paint mPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF mTrackRect = new RectF();
    private final RectF mFillRect = new RectF();

    private int mMin = 0;
    private int mMax = 100;
    private int mValue = 50;
    private boolean mDragging = false;
    private Shader mGradShader;
    private int mLastGradWidth = -1;
    private OnValueChangeListener mListener;

    public NeonSliderView(Context context) {
        this(context, null);
    }

    public NeonSliderView(Context context, AttributeSet attrs) {
        super(context, attrs);
    }

    public void setOnValueChangeListener(OnValueChangeListener listener) {
        mListener = listener;
    }

    public void setRange(int min, int max) {
        mMin = min;
        mMax = Math.max(min + 1, max);
        setValue(mValue, false);
    }

    public int getValue() {
        return mValue;
    }

    public void setValue(int value, boolean notify) {
        int clamped = Math.max(mMin, Math.min(mMax, value));
        if (mValue == clamped) return;
        mValue = clamped;
        invalidate();
        if (notify && mListener != null) {
            mListener.onValueChanged(this, mValue, false);
        }
    }

    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        float d = getResources().getDisplayMetrics().density;
        int w = resolveSize(Math.round(240 * d), widthMeasureSpec);
        int h = resolveSize(Math.round(30 * d), heightMeasureSpec);
        setMeasuredDimension(w, h);
    }

    private void updateFromX(float x, boolean notify) {
        float d = getResources().getDisplayMetrics().density;
        float pad = Math.max(12f * d, getHeight() * 0.45f);
        float trackW = Math.max(1f, getWidth() - pad * 2f);
        float norm = Math.max(0f, Math.min(1f, (x - pad) / trackW));
        int next = Math.round(mMin + norm * (mMax - mMin));
        if (next != mValue) {
            mValue = next;
            invalidate();
            if (notify && mListener != null) {
                mListener.onValueChanged(this, mValue, false);
            }
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                mDragging = true;
                if (getParent() != null) {
                    getParent().requestDisallowInterceptTouchEvent(true);
                }
                updateFromX(event.getX(), true);
                invalidate();
                return true;
            case MotionEvent.ACTION_MOVE:
                if (mDragging) {
                    updateFromX(event.getX(), true);
                }
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                if (mDragging) {
                    mDragging = false;
                    updateFromX(event.getX(), true);
                    invalidate();
                    if (mListener != null) {
                        mListener.onValueChanged(this, mValue, true);
                    }
                }
                return true;
            default:
                return super.onTouchEvent(event);
        }
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float d = getResources().getDisplayMetrics().density;
        float pad = Math.max(12f * d, getHeight() * 0.45f);
        float trackH = Math.max(4f * d, getHeight() * 0.22f);
        float trackTop = (getHeight() - trackH) * 0.5f;
        float trackW = Math.max(10f, getWidth() - pad * 2f);

        mTrackRect.set(pad, trackTop, pad + trackW, trackTop + trackH);
        float r = trackH * 0.5f;

        mPaint.setShader(null);
        mPaint.setStyle(Paint.Style.FILL);
        mPaint.setColor(Color.rgb(38, 22, 70));
        canvas.drawRoundRect(mTrackRect, r, r, mPaint);

        float norm = (float) (mValue - mMin) / Math.max(1, mMax - mMin);
        float fillW = trackW * norm;
        if (fillW > 0.5f) {
            if (mGradShader == null || mLastGradWidth != getWidth()) {
                mLastGradWidth = getWidth();
                mGradShader = new LinearGradient(
                        pad, 0f, pad + trackW, 0f,
                        Color.rgb(255, 42, 133),
                        Color.rgb(0, 240, 255),
                        Shader.TileMode.CLAMP);
            }
            mFillRect.set(pad, trackTop, pad + fillW, trackTop + trackH);
            mPaint.setShader(mGradShader);
            canvas.drawRoundRect(mFillRect, r, r, mPaint);
            mPaint.setShader(null);
        }

        float thumbX = pad + fillW;
        float thumbY = getHeight() * 0.5f;
        float thumbR = Math.max(7f * d, (getHeight() - 10f * d) * 0.38f);

        if (mDragging) {
            mPaint.setColor(Color.argb(80, 0, 240, 255));
            canvas.drawCircle(thumbX, thumbY, thumbR + 5f * d, mPaint);
        }

        mPaint.setColor(Color.WHITE);
        canvas.drawCircle(thumbX, thumbY, thumbR, mPaint);

        mPaint.setStyle(Paint.Style.STROKE);
        mPaint.setStrokeWidth(2f * d);
        mPaint.setColor(Color.rgb(0, 240, 255));
        canvas.drawCircle(thumbX, thumbY, thumbR, mPaint);
        mPaint.setStyle(Paint.Style.FILL);
    }
}
