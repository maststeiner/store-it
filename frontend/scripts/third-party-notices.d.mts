// Types for the build script (SPEC-009), so its unit test is type-checked.
export interface ExtractedLicense {
  name: string;
  license: string;
  text: string | null;
}

export interface ThirdPartyNotice extends ExtractedLicense {
  version: string | null;
  copyright: null;
  url: string | null;
}

export const EXTRACTION_FILE: string;
export const OUTPUT_FILE: string;
export function parseLicenseExtraction(extraction: string): ExtractedLicense[];
export function enrich(
  entries: ExtractedLicense[],
  readPackageJson: (name: string) => Record<string, unknown> | null,
): ThirdPartyNotice[];
export function main(distDir: string, nodeModulesDir: string): number;
