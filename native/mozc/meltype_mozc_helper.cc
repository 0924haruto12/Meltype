// Meltype 用の Mozc 変換ヘルパー。
// Copyright (C) 2026 Yukishiro. Mozc 本体は Google LLC の BSD-3-Clause ライセンス。
//
// 標準入力から 1 行に 1 つの要求を受け取り、標準出力に 1 行で答える (UTF-8)。
//   C<TAB>前の文字列<TAB>読み   → 変換。文節ごとに「読み<US>候補1<US>候補2…」を <RS> でつないで返す
//   (US = 0x1F, RS = 0x1E)。変換できなければ空行。
//   Q                            → 終了。
// 起動が終わったら "READY" を 1 行出す。引数 1 つ目は学習データの保存先 (省略可)。

#include <iostream>
#include <memory>
#include <string>
#include <utility>
#include <vector>

#include "absl/strings/str_split.h"
#include "absl/strings/string_view.h"
#include "base/init_mozc.h"
#include "base/system_util.h"
#include "composer/composer.h"
#include "config/config_handler.h"
#include "converter/candidate.h"
#include "converter/converter_interface.h"
#include "converter/segments.h"
#include "engine/engine.h"
#include "engine/engine_factory.h"
#include "protocol/commands.pb.h"
#include "protocol/config.pb.h"
#include "request/conversion_request.h"

#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#endif

namespace {

constexpr char kUnitSeparator = '\x1f';
constexpr char kRecordSeparator = '\x1e';

std::string Convert(const mozc::ConverterInterface& converter,
                    const mozc::commands::Request& request,
                    const mozc::config::Config& config,
                    mozc::composer::Composer& composer,
                    absl::string_view context, absl::string_view reading) {
  mozc::Segments segments;
  if (!context.empty()) {
    // 前の文字列を文脈にする (失敗しても変換は続ける)。
    (void)converter.ReconstructHistory(&segments, context);
  }
  composer.Reset();
  composer.SetPreeditTextForTestOnly(reading);
  mozc::ConversionRequest::Options options = {
      .request_type = mozc::ConversionRequest::CONVERSION,
      .max_conversion_candidates_size = 40,
      .create_partial_candidates = false,
  };
  const mozc::ConversionRequest conversion_request =
      mozc::ConversionRequestBuilder()
          .SetComposer(composer)
          .SetRequestView(request)
          .SetConfigView(config)
          .SetOptions(std::move(options))
          .Build();
  if (!converter.StartConversion(conversion_request, &segments)) return "";

  std::string out;
  for (size_t i = 0; i < segments.conversion_segments_size(); ++i) {
    const mozc::Segment& segment = segments.conversion_segment(i);
    if (i > 0) out += kRecordSeparator;
    out += segment.key();
    for (size_t j = 0; j < segment.candidates_size(); ++j) {
      out += kUnitSeparator;
      out += segment.candidate(j).value;
    }
  }
  return out;
}

}  // namespace

int main(int argc, char** argv) {
  mozc::InitMozc(argv[0], &argc, &argv);
#ifdef _WIN32
  _setmode(_fileno(stdin), _O_BINARY);
  _setmode(_fileno(stdout), _O_BINARY);
#endif
  if (argc >= 2 && argv[1][0] != '\0') {
    mozc::SystemUtil::SetUserProfileDirectory(argv[1]);
  }

  auto engine = mozc::EngineFactory::Create();
  if (!engine.ok()) {
    std::cout << "ERROR " << engine.status().message() << std::endl;
    return 1;
  }
  std::unique_ptr<mozc::Engine> owned = *std::move(engine);
  const mozc::commands::Request request;
  const mozc::config::Config config = mozc::config::ConfigHandler::DefaultConfig();
  auto converter = owned->GetConverter();
  mozc::composer::Composer composer(request, config);

  std::cout << "READY" << std::endl;
  std::string line;
  while (std::getline(std::cin, line)) {
    if (!line.empty() && line.back() == '\r') line.pop_back();
    const std::vector<absl::string_view> fields = absl::StrSplit(line, '\t');
    if (fields.empty() || fields[0] == "Q") break;
    if (fields[0] == "C" && fields.size() >= 3) {
      std::cout << Convert(*converter, request, config, composer, fields[1], fields[2]) << "\n";
    } else {
      std::cout << "\n";
    }
    std::cout.flush();
  }
  return 0;
}
