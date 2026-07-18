"use client";

import Image from "next/image";
import { useState } from "react";

const LOGO_CANDIDATES = [
  "/logo/mtglory_logo.png",
  "/images/logo/mtglory_logo.png",
  "/images/logo/logo.png",
  "/images/logo/logo.svg",
] as const;

type CompanyLogoProps = {
  size?: number;
  className?: string;
  showText?: boolean;
  productName?: string;
  tagline?: string;
  /** light = for dark backgrounds (header/footer), dark = for light backgrounds */
  tone?: "light" | "dark";
};

export function CompanyLogo({
  size = 44,
  className = "",
  showText = true,
  productName = "Glory Desk",
  tagline = "MT GLORY CO",
  tone = "light",
}: CompanyLogoProps) {
  const [srcIndex, setSrcIndex] = useState(0);
  const [failed, setFailed] = useState(false);

  const mark = failed ? (
    <div
      className="logo-mark logo-mark-fallback"
      style={{ width: size, height: size, fontSize: size * 0.32 }}
      aria-hidden
    >
      MT
    </div>
  ) : (
    <Image
      src={LOGO_CANDIDATES[srcIndex]}
      alt="MT GLORY CO logo"
      width={size}
      height={size}
      className="company-logo-img"
      onError={() => {
        if (srcIndex < LOGO_CANDIDATES.length - 1) {
          setSrcIndex((i) => i + 1);
        } else {
          setFailed(true);
        }
      }}
      priority
    />
  );

  if (!showText) {
    return <span className={`company-logo ${className}`.trim()}>{mark}</span>;
  }

  return (
    <span className={`company-logo ${className}`.trim()}>
      {mark}
      <span className={`logo-text logo-text-${tone}`}>
        <strong>{productName}</strong>
        <span>{tagline}</span>
      </span>
    </span>
  );
}

export function CompanyLogoMark({ size = 44, className = "" }: { size?: number; className?: string }) {
  return <CompanyLogo size={size} showText={false} className={className} />;
}
