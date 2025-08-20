// float SplineKernel(float dst, float h)
// {
//     float PI = 3.1415927;
//     float q = dst / h;
//     if (2 < q) { return 0; }
//     float scale =  10 / (7 * PI * h * h);
//     if (1 < q) 
//     { return scale * (2-q) * (2-q) / 4;}
//     return scale * (1 - 1.5*q*q * (1 - 0.5*q));
// }

// float SplineKernelDerivative(float dst, float h)
// {
//     float PI = 3.1415927;
//     float q = dst / h;
//     if (2 < q) { return 0; }
//     float scale =  15 / (7 * PI * h * h * h);
//     if (1 < q) 
//     { return scale * (q-2) / 2;}
//     return scale * (-3*q + 2.25*q*q);
// }

float SplineKernel(float q, float scale)
{
    if (2 < q) { return 0; }
    return scale * (1+2*q)*(2-q)*(2-q)*(2-q)*(2-q);
}

float SplineKernelDerivative(float q, float scale)
{
    if (2 < q) { return 0; }
    return - scale * (2-q)*(2-q)*(2-q)*q;
}

float ConvertPressureToDensity(float pressure, float targetDensity, float refrencePressure, float gamma)
{
    return targetDensity * pow(abs(pressure / refrencePressure + 1), 1/gamma);
}

float ConvertDensityToPressure(float density, float targetDensity, float refrencePressure, float gamma)
{
    return refrencePressure * (pow(abs(density / targetDensity), gamma) - 1);
}