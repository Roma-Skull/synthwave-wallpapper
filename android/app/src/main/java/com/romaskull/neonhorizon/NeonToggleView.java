package com.romaskull.neonhorizon;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.AttributeSet;
import android.view.View;

public final class NeonToggleView extends View {
    public interface OnCheckedChangeListener {
        void onCheckedChanged(NeonToggleView view, boolean checked);
    }

    private final Paint mPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF mTrackRect = new RectF();
    private boolean mChecked = true;
    private OnCheckedChangeListener mListener;

    public NeonToggleView(Context context) {
        this(context, null);
    }

    public NeonToggleView(Context context, AttributeSet attrs) {
        super(context, attrs);
        setClickable(true);
        setFocusable(true);
        setOnClickListener(v -> setChecked(!mChecked, true));
    }

    public void setOnCheckedChangeListener(OnCheckedChangeListener listener) {
        mListener = listener;
    }

    public boolean isChecked() {
        return mChecked;
    }

    public void setChecked(boolean checked, boolean notify) {
        if (mChecked == checked) return;
        mChecked = checked;
        invalidate();
        if (notify && mListener != null) {
            mListener.onCheckedChanged(this, mChecked);
        }
    }

    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        float d = getResources().getDisplayMetrics().density;
        int w = resolveSize(Math.round(52 * d), widthMeasureSpec);
        int h = resolveSize(Math.round(28 * d), heightMeasureSpec);
        setMeasuredDimension(w, h);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float d = getResources().getDisplayMetrics().density;
        float stroke = Math.max(1.5f, 1.5f * d);
        mTrackRect.set(stroke, stroke, getWidth() - stroke, getHeight() - stroke);
        float radius = mTrackRect.height() * 0.5f;

        mPaint.setStyle(Paint.Style.FILL);
        mPaint.setColor(mChecked ? Color.argb(64, 0, 240, 255) : Color.rgb(36, 20, 66));
        canvas.drawRoundRect(mTrackRect, radius, radius, mPaint);

        mPaint.setStyle(Paint.Style.STROKE);
        mPaint.setStrokeWidth(stroke);
        mPaint.setColor(mChecked ? Color.rgb(0, 240, 255) : Color.argb(145, 181, 55, 242));
        canvas.drawRoundRect(mTrackRect, radius, radius, mPaint);

        float pad = 4f * d;
        float thumbR = (getHeight() - pad * 2f) * 0.5f;
        float thumbCx = mChecked ? (getWidth() - pad - thumbR) : (pad + thumbR);
        float thumbCy = getHeight() * 0.5f;

        mPaint.setStyle(Paint.Style.FILL);
        mPaint.setColor(mChecked ? Color.rgb(0, 240, 255) : Color.rgb(196, 178, 232));
        canvas.drawCircle(thumbCx, thumbCy, thumbR, mPaint);
    }
}
