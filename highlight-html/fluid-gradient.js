import * as THREE from "./three.module.js";

/**
 * Fluid gradient background:
 * - Fullscreen quad
 * - Fragment shader blends a small set of moving colored blobs (metaball-ish)
 * - Subtle domain warping for a more "liquid" Apple Music vibe
 */

function rgb01(r, g, b) { return { r: r / 255, g: g / 255, b: b / 255 }; }

function clamp255(v) { return Math.max(0, Math.min(255, v)); }

function normalizeRgbLike(color) {
    // Accept: {r,g,b} in 0..255, [r,g,b] in 0..255, or CSS "#rrggbb" / "rgb()".
    if (!color) return { r: 103, g: 159, b: 206 };

    if (Array.isArray(color) && color.length >= 3) {
        return { r: clamp255(color[0]), g: clamp255(color[1]), b: clamp255(color[2]) };
    }

    if (typeof color === "string") {
        const s = color.trim();
        if (s.startsWith("#")) {
            const hex = s.slice(1);
            if (hex.length === 3) {
                const r = parseInt(hex[0] + hex[0], 16);
                const g = parseInt(hex[1] + hex[1], 16);
                const b = parseInt(hex[2] + hex[2], 16);
                return { r, g, b };
            }
            if (hex.length === 6) {
                const r = parseInt(hex.slice(0, 2), 16);
                const g = parseInt(hex.slice(2, 4), 16);
                const b = parseInt(hex.slice(4, 6), 16);
                return { r, g, b };
            }
        }

        // rgb(r,g,b)
        const m = s.match(/rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)/i);
        if (m) return { r: clamp255(+m[1]), g: clamp255(+m[2]), b: clamp255(+m[3]) };
    }

    if (typeof color === "object" && color.r != null && color.g != null && color.b != null) {
        return { r: clamp255(color.r), g: clamp255(color.g), b: clamp255(color.b) };
    }

    return { r: 103, g: 159, b: 206 };
}

function randomPastelNearBase(baseRGB, variation = 40, pastelMix = 0.35) {
    const r = clamp255(baseRGB.r + Math.floor(Math.random() * (variation * 2 + 1)) - variation);
    const g = clamp255(baseRGB.g + Math.floor(Math.random() * (variation * 2 + 1)) - variation);
    const b = clamp255(baseRGB.b + Math.floor(Math.random() * (variation * 2 + 1)) - variation);

    // push toward pastel by mixing with white a bit
    const mix = Math.max(0, Math.min(1, pastelMix));
    const pr = Math.round(r + (255 - r) * mix);
    const pg = Math.round(g + (255 - g) * mix);
    const pb = Math.round(b + (255 - b) * mix);

    return rgb01(pr, pg, pb);
}

function generatePalette01({ referenceColor, blobCount, variation, pastelMix }) {
    const base = normalizeRgbLike(referenceColor);
    const colors = new Float32Array(blobCount * 3);
    for (let i = 0; i < blobCount; i++) {
        const c = randomPastelNearBase(base, variation, pastelMix);
        colors[i * 3 + 0] = c.r;
        colors[i * 3 + 1] = c.g;
        colors[i * 3 + 2] = c.b;
    }
    return colors;
}

function generateSeeds(blobCount, rng = Math.random) {
    // Stable-ish random seeds per blob (so motion is deterministic, not jittery)
    const seeds = new Float32Array(blobCount * 4);
    for (let i = 0; i < blobCount; i++) {
        seeds[i * 4 + 0] = rng() * 1000;
        seeds[i * 4 + 1] = rng() * 1000;
        seeds[i * 4 + 2] = 0.6 + rng() * 0.7;   // speed
        seeds[i * 4 + 3] = 0.10 + rng() * 0.18; // radius-ish
    }
    return seeds;
}

const vertexShader = /* glsl */ `
  varying vec2 vUv;
  void main() {
    vUv = uv;
    gl_Position = vec4(position.xy, 0.0, 1.0);
  }
`;

function buildFragmentShader(blobCount) {
    return /* glsl */ `
  precision highp float;

  varying vec2 vUv;

  uniform float uTime;
  uniform vec2  uResolution;
  uniform int   uBlobCount;

  uniform float uWarpStrength;
  uniform float uSharpness;
  uniform float uVignette;

  uniform float uColorMix; // 0..1 blend uColorsA->uColorsB

  // Packed arrays: [r,g,b,r,g,b,...]
  uniform float uColorsA[${blobCount * 3}];
  uniform float uColorsB[${blobCount * 3}];
  // Packed seeds: [sx,sy,speed,radius,...]
  uniform float uSeeds[${blobCount * 4}];

  // Tiny hash/noise helpers (fast, not "true" Perlin)
  float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 34.345);
    return fract(p.x * p.y);
  }

  float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
  }

  vec2 warp(vec2 uv, float t) {
    // 2-layer value-noise warp
    float n1 = noise(uv * 2.0 + vec2(t * 0.10, -t * 0.08));
    float n2 = noise(uv * 4.0 + vec2(-t * 0.07, t * 0.06));
    vec2 dir = vec2(n1 - 0.5, n2 - 0.5);
    return uv + dir * uWarpStrength * 0.25;
  }

  vec2 blobPos(int i, float t) {
    // Smooth looping Lissajous-ish motion, unique per blob
    float sx = uSeeds[i * 4 + 0];
    float sy = uSeeds[i * 4 + 1];
    float sp = uSeeds[i * 4 + 2];

    float a = 0.6 + fract(sx * 0.001) * 1.4;
    float b = 0.7 + fract(sy * 0.001) * 1.3;

    float x = 0.5 + 0.38 * sin(t * sp * a + sx);
    float y = 0.5 + 0.38 * cos(t * sp * b + sy);

    return vec2(x, y);
  }

  vec3 blobColor(int i) {
    vec3 a = vec3(
      uColorsA[i * 3 + 0],
      uColorsA[i * 3 + 1],
      uColorsA[i * 3 + 2]
    );
    vec3 b = vec3(
      uColorsB[i * 3 + 0],
      uColorsB[i * 3 + 1],
      uColorsB[i * 3 + 2]
    );
    return mix(a, b, clamp(uColorMix, 0.0, 1.0));
  }

  float blobRadius(int i) {
    return uSeeds[i * 4 + 3];
  }

  void main() {
    float t = uTime;

    // Account for aspect so blobs are circular on screen
    vec2 uv = vUv;
    float aspect = uResolution.x / max(1.0, uResolution.y);

    // Warp UVs first (helps fluid look)
    vec2 wuv = warp(uv, t);
    vec2 p = wuv;
    p.x = (p.x - 0.5) * aspect + 0.5;

    vec3 accum = vec3(0.0);
    float wsum = 0.0;

    for (int i = 0; i < ${blobCount}; i++) {
      if (i >= uBlobCount) break;

      vec2 c = blobPos(i, t);
      c.x = (c.x - 0.5) * aspect + 0.5;

      float r = blobRadius(i);

      float d = distance(p, c);
      // Gaussian-ish weight; uSharpness controls falloff
      float w = exp(-pow(d / r, uSharpness));

      accum += blobColor(i) * w;
      wsum += w;
    }

    vec3 col = accum / max(1e-5, wsum);

    // Gentle contrast boost
    col = pow(col, vec3(0.85));

    // Vignette
    vec2 q = vUv - 0.5;
    float vig = smoothstep(0.85, 0.15, dot(q, q) * 2.0);
    col *= mix(1.0 - uVignette, 1.0, vig);

    // Slight dark base so whites don't blow out
    col = clamp(col, 0.0, 1.0);

    gl_FragColor = vec4(col, 1.0);
  }
  `;
}

export class FluidGradient {
    /**
     * @param {object} options
     * @param {HTMLElement|string} [options.container=".bg"] Element or selector to host the canvas. A selector may match multiple elements.
     * @param {{r:number,g:number,b:number}|number[]|string} [options.referenceColor={r:103,g:159,b:206}] Base color for palette generation.
     * @param {number} [options.blobCount=10] Number of blobs (requires shader rebuild when changed).
     * @param {number} [options.variation=40] Random variation around reference color.
     * @param {number} [options.pastelMix=0.35] How much to mix generated colors toward white (0..1).
     * @param {number} [options.warpStrength=0.20]
     * @param {number} [options.sharpness=1.35]
     * @param {number} [options.vignette=0.30]
     * @param {number} [options.pixelRatioCap=2] Max devicePixelRatio to render at.
     */
    constructor(options = {}) {
        const {
            container = ".bg",
            referenceColor = { r: 103, g: 159, b: 206 },
            blobCount = 10,
            variation = 40,
            pastelMix = 0.35,
            warpStrength = 0.20,
            sharpness = 1.35,
            vignette = 0.30,
            pixelRatioCap = 2,
        } = options;

        const el = typeof container === "string" ? document.querySelector(container) : container;
        if (!el) {
            throw new Error("FluidGradient: container element not found.");
        }

        this.container = el;
        this.blobCount = Math.max(1, Math.min(64, blobCount | 0));
        this.variation = variation;
        this.pastelMix = pastelMix;

        // ---- Three.js minimal setup (fullscreen quad)
        this.scene = new THREE.Scene();
        this.camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);

        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: "high-performance" });
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, pixelRatioCap));
        this.container.appendChild(this.renderer.domElement);

        const colorsA = generatePalette01({
            referenceColor,
            blobCount: this.blobCount,
            variation: this.variation,
            pastelMix: this.pastelMix,
        });
        const colorsB = new Float32Array(colorsA); // start identical
        const seeds = generateSeeds(this.blobCount);

        this.uniforms = {
            uTime: { value: 0 },
            uResolution: { value: new THREE.Vector2(1, 1) },
            uBlobCount: { value: this.blobCount },
            uColorsA: { value: colorsA },
            uColorsB: { value: colorsB },
            uColorMix: { value: 0 },
            uSeeds: { value: seeds },
            // feel knobs
            uWarpStrength: { value: warpStrength },
            uSharpness: { value: sharpness },
            uVignette: { value: vignette },
        };

        this.material = new THREE.ShaderMaterial({
            uniforms: this.uniforms,
            vertexShader,
            fragmentShader: buildFragmentShader(this.blobCount),
        });

        this.quad = new THREE.Mesh(new THREE.PlaneGeometry(2, 2), this.material);
        this.scene.add(this.quad);

        this._blend = null; // {startT, duration}
        this._onResize = this._onResize.bind(this);
        window.addEventListener("resize", this._onResize);
        this._onResize();
    }

    dispose() {
        window.removeEventListener("resize", this._onResize);
        this.quad.geometry.dispose();
        this.material.dispose();
        this.renderer.dispose();
        if (this.renderer.domElement && this.renderer.domElement.parentNode) {
            this.renderer.domElement.parentNode.removeChild(this.renderer.domElement);
        }
    }

    _onResize() {
        const w = window.innerWidth;
        const h = window.innerHeight;
        this.renderer.setSize(w, h, false);
        this.uniforms.uResolution.value.set(w * this.renderer.getPixelRatio(), h * this.renderer.getPixelRatio());
    }

    /**
     * Render one frame.
     * @param {number} tSeconds Time in seconds (what you asked for vs ms-based rAF).
     */
    update(tSeconds) {
        // Keep the original vibe: old code used t(ms) * 0.0002.
        // With seconds input, multiply by 0.2 to match: (ms*0.0002) == (s*1000*0.0002) == s*0.2
        this.uniforms.uTime.value = tSeconds * 0.2;

        if (this._blend) {
            const { startT, duration } = this._blend;
            const p = duration <= 0 ? 1 : Math.max(0, Math.min(1, (tSeconds - startT) / duration));
            this.uniforms.uColorMix.value = p;
            if (p >= 1) {
                // commit: B becomes A, reset mix
                this.uniforms.uColorsA.value.set(this.uniforms.uColorsB.value);
                this.uniforms.uColorMix.value = 0;
                this._blend = null;
            }
        }

        this.renderer.render(this.scene, this.camera);
    }

    /**
     * Sets the reference color used for generating new palettes.
     * This does not automatically change the current palette; call updateColors() to apply.
     */
    setReferenceColor(referenceColor, { variation, pastelMix } = {}) {
        this.referenceColor = normalizeRgbLike(referenceColor);
        if (variation != null) this.variation = variation;
        if (pastelMix != null) this.pastelMix = pastelMix;
    }

    /**
     * Regenerates the blob colors and optionally blends to them over time.
     * @param {{r:number,g:number,b:number}|number[]|string|Float32Array} [referenceOrColors]
     *   - If Float32Array: treated as palette in 0..1 packed [r,g,b,...] length blobCount*3.
     *   - Else: treated as reference color.
     * @param {{blendSeconds?:number, variation?:number, pastelMix?:number, nowSeconds?:number}} [options]
     */
    updateColors(referenceOrColors, options = {}) {
        const { blendSeconds = 0, variation = this.variation, pastelMix = this.pastelMix, nowSeconds = 0 } = options;

        let next;
        if (referenceOrColors instanceof Float32Array) {
            if (referenceOrColors.length !== this.blobCount * 3) {
                throw new Error(`FluidGradient.updateColors: expected Float32Array length ${this.blobCount * 3}.`);
            }
            next = referenceOrColors;
        } else {
            const ref = referenceOrColors != null ? referenceOrColors : (this.referenceColor || { r: 103, g: 159, b: 206 });
            next = generatePalette01({ referenceColor: ref, blobCount: this.blobCount, variation, pastelMix });
        }

        // write into B buffer
        this.uniforms.uColorsB.value.set(next);

        if (blendSeconds > 0) {
            this._blend = { startT: nowSeconds, duration: blendSeconds };
        } else {
            this.uniforms.uColorsA.value.set(this.uniforms.uColorsB.value);
            this.uniforms.uColorMix.value = 0;
            this._blend = null;
        }
    }
}

// Backward-compatible auto-run for the existing demo page.
// Instantiates a gradient for each matching `.bg` container and updates them all.
const _autoContainers = document.querySelectorAll(".bg");
if (_autoContainers.length > 0) {
    window.fluidGradients = [];
    _autoContainers.forEach((c) => {
        const gradient = new FluidGradient({ container: c, referenceColor: "#FF022E", pastelMix: 0.2 });
        window.fluidGradients.push(gradient);
    });
    window.fluidGradient = window.fluidGradients[0]; // legacy alias
    window.dispatchEvent(new Event('fluidGradientReady'));

    function animate(ms) {
        const t = ms / 1000;
        window.fluidGradients.forEach((g) => g.update(t));
        requestAnimationFrame(animate);
    }

    requestAnimationFrame(animate);
}
